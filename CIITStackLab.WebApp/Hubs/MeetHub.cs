using System.Collections.Concurrent;
using System.Security.Claims;
using CIITStackLab.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace CIITStackLab.WebApp.Hubs;

[Authorize]
public sealed class MeetHub : Hub
{
    private readonly ApplicationDbContext _dbContext;
    private static readonly ConcurrentDictionary<string, int> PeerSessions = new();

    public MeetHub(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public static string SessionGroup(int sessionId) => $"meet-session-{sessionId}";
    public static string AdminSessionGroup(int sessionId) => $"meet-session-admins-{sessionId}";
    public static string BatchGroup(int batchId) => $"meet-batch-{batchId}";
    public const string AdminGroup = "meet-admins";
    public static string PeerGroup(int sessionId) => $"meet-session-peers-{sessionId}";

    public async Task JoinAdminUpdates()
    {
        if (!Context.User!.IsInRole("Admin") && !Context.User.IsInRole("Super User"))
            throw new HubException("Access denied.");

        await Groups.AddToGroupAsync(Context.ConnectionId, AdminGroup, Context.ConnectionAborted);

        var currentSessions = await _dbContext.MeetSessions
            .AsNoTracking()
            .Where(session => !session.MeetBatch.IsArchived)
            .Select(session => new
            {
                sessionId = session.Id,
                batchId = session.MeetBatchId,
                session.Title,
                session.Status,
                startsAtUtc = session.StartsAtUtc,
                startedAtUtc = session.StartedAtUtc,
                session.DurationMinutes
            })
            .ToListAsync(Context.ConnectionAborted);

        foreach (var session in currentSessions)
            await Clients.Caller.SendAsync("MeetAdminUpdated", session, Context.ConnectionAborted);
    }

    public async Task JoinMyBatchUpdates()
    {
        var studentId = Context.UserIdentifier;
        if (string.IsNullOrWhiteSpace(studentId) || !Context.User!.IsInRole("Student"))
            throw new HubException("Access denied.");

        var batchIds = await _dbContext.MeetBatchMembers
            .AsNoTracking()
            .Where(member => member.StudentUserId == studentId && !member.MeetBatch.IsArchived)
            .Select(member => member.MeetBatchId)
            .ToListAsync(Context.ConnectionAborted);

        foreach (var batchId in batchIds)
            await Groups.AddToGroupAsync(Context.ConnectionId, BatchGroup(batchId), Context.ConnectionAborted);

        var now = DateTime.UtcNow;
        var currentSessions = await _dbContext.MeetSessions
            .AsNoTracking()
            .Where(session => batchIds.Contains(session.MeetBatchId)
                && !session.MeetBatch.IsArchived
                && ((session.Status == "Live"
                        && session.StartedAtUtc.HasValue
                        && session.StartedAtUtc.Value.AddMinutes(session.DurationMinutes) > now)
                    || (session.Status == "Scheduled" && session.StartsAtUtc > now)))
            .Select(session => new
            {
                sessionId = session.Id,
                batchId = session.MeetBatchId,
                session.Title,
                session.Status,
                startsAtUtc = session.StartsAtUtc,
                startedAtUtc = session.StartedAtUtc,
                session.DurationMinutes
            })
            .ToListAsync(Context.ConnectionAborted);

        // A batch card can display only one action. Choose exactly one valid
        // live session when present; otherwise show the nearest upcoming session.
        // The same ordering is used by the server-rendered Student Meet page.
        var preferredSessions = currentSessions
            .GroupBy(session => session.batchId)
            .Select(group => group
                .OrderByDescending(session => session.Status == "Live")
                .ThenByDescending(session => session.Status == "Live"
                    ? session.startedAtUtc
                    : session.startsAtUtc)
                .ThenByDescending(session => session.sessionId)
                .First());

        foreach (var session in preferredSessions)
            await Clients.Caller.SendAsync("MeetBatchUpdated", session, Context.ConnectionAborted);
    }

    public async Task JoinMeetingRoom(int sessionId)
    {
        if (sessionId <= 0)
            throw new HubException("Invalid meeting.");

        var session = await _dbContext.MeetSessions
            .AsNoTracking()
            .Where(item => item.Id == sessionId && !item.MeetBatch.IsArchived)
            .Select(item => new
            {
                item.Id,
                item.MeetBatchId,
                item.Status,
                item.StartedAtUtc,
                item.DurationMinutes
            })
            .SingleOrDefaultAsync(Context.ConnectionAborted);

        if (session is null)
            throw new HubException("Meeting not found.");

        var isAdmin = Context.User!.IsInRole("Admin") || Context.User.IsInRole("Super User");
        if (!isAdmin)
        {
            var studentId = Context.UserIdentifier;
            if (string.IsNullOrWhiteSpace(studentId)
                || !Context.User.IsInRole("Student")
                || session.Status != "Live"
                || !session.StartedAtUtc.HasValue
                || session.StartedAtUtc.Value.AddMinutes(session.DurationMinutes) <= DateTime.UtcNow
                || !await _dbContext.MeetBatchMembers.AsNoTracking().AnyAsync(
                    member => member.MeetBatchId == session.MeetBatchId
                        && member.StudentUserId == studentId,
                    Context.ConnectionAborted)
                || !await _dbContext.MeetSessionParticipants.AsNoTracking().AnyAsync(
                    participant => participant.MeetSessionId == sessionId
                        && participant.StudentUserId == studentId
                        && (participant.Status == "Pending" || participant.Status == "Admitted"),
                    Context.ConnectionAborted))
            {
                throw new HubException("You are not allowed to join this meeting.");
            }
        }

        if (isAdmin)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, AdminSessionGroup(sessionId), Context.ConnectionAborted);

            var adminSession = await _dbContext.MeetSessions
                .AsNoTracking()
                .Where(item => item.Id == sessionId)
                .Select(item => new
                {
                    item.Id,
                    item.MeetBatchId,
                    item.Title,
                    item.Status,
                    item.StartsAtUtc,
                    item.StartedAtUtc,
                    item.EndedAtUtc,
                    item.CancelledAtUtc,
                    item.DurationMinutes,
                    item.WaitingRoomEnabled,
                    item.RecordingEnabled,
                    item.RecordingStatus,
                    item.RecordingRetentionUntilUtc
                })
                .SingleAsync(Context.ConnectionAborted);

            var adminParticipants = await (
                from participant in _dbContext.MeetSessionParticipants.AsNoTracking()
                join user in _dbContext.Users.AsNoTracking() on participant.StudentUserId equals user.Id
                where participant.MeetSessionId == sessionId
                orderby participant.RequestedAtUtc
                select new
                {
                    participant.Id,
                    StudentName = user.UserName ?? user.Email ?? "Student",
                    participant.Status,
                    participant.CanUseMicrophone,
                    participant.CanUseCamera,
                    participant.CanShareScreen
                }).ToListAsync(Context.ConnectionAborted);

            await Clients.Caller.SendAsync("MeetRoomUpdated", new
            {
                sessionId = adminSession.Id,
                batchId = adminSession.MeetBatchId,
                adminSession.Title,
                adminSession.Status,
                startsAtUtc = adminSession.StartsAtUtc,
                startedAtUtc = adminSession.StartedAtUtc,
                endedAtUtc = adminSession.EndedAtUtc,
                cancelledAtUtc = adminSession.CancelledAtUtc,
                adminSession.DurationMinutes,
                adminSession.WaitingRoomEnabled,
                adminSession.RecordingEnabled,
                adminSession.RecordingStatus,
                recordingRetentionUntilUtc = adminSession.RecordingRetentionUntilUtc,
                pendingCount = adminParticipants.Count(item => item.Status == "Pending"),
                participantCount = adminParticipants.Count(item => item.Status == "Admitted"),
                participants = adminParticipants
            }, Context.ConnectionAborted);

            var pendingCount = adminParticipants.Count(item => item.Status == "Pending");
            if (pendingCount > 0 && adminSession.Status == "Live")
            {
                await Clients.Caller.SendAsync("MeetToast", new
                {
                    title = "Join requests waiting",
                    message = $"{pendingCount} student(s) are waiting for your approval.",
                    type = "info",
                    createdAtUtc = DateTime.UtcNow
                }, Context.ConnectionAborted);
            }
        }
        else
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, SessionGroup(sessionId), Context.ConnectionAborted);
        }

        var isActive = session.Status == "Live"
            && session.StartedAtUtc.HasValue
            && session.StartedAtUtc.Value.AddMinutes(session.DurationMinutes) > DateTime.UtcNow;
        var participantAccess = isAdmin ? null : await _dbContext.MeetSessionParticipants
            .AsNoTracking()
            .Where(participant => participant.MeetSessionId == sessionId
                && participant.StudentUserId == Context.UserIdentifier
                && (participant.Status == "Pending" || participant.Status == "Admitted"))
            .Select(participant => new
            {
                participant.Status,
                participant.CanUseMicrophone,
                participant.CanUseCamera,
                participant.CanShareScreen
            })
            .SingleOrDefaultAsync(Context.ConnectionAborted);
        var isAdmittedStudent = isAdmin || (isActive && participantAccess?.Status == "Admitted");

        if (!isAdmin && participantAccess is not null)
        {
            await Clients.Caller.SendAsync("MeetMyParticipantStatus", new
            {
                sessionId,
                status = participantAccess.Status,
                sessionStatus = session.Status,
                canUseMicrophone = participantAccess.CanUseMicrophone,
                canUseCamera = participantAccess.CanUseCamera,
                canShareScreen = participantAccess.CanShareScreen
            }, Context.ConnectionAborted);
        }

        if (isActive && isAdmittedStudent)
        {
            await EnsurePeerAccessAsync(sessionId);
            await Groups.AddToGroupAsync(Context.ConnectionId, PeerGroup(sessionId), Context.ConnectionAborted);
            PeerSessions[Context.ConnectionId] = sessionId;

            var existingPeerIds = PeerSessions
                .Where(pair => pair.Value == sessionId && pair.Key != Context.ConnectionId)
                .Select(pair => pair.Key)
                .ToArray();

            await Clients.Caller.SendAsync("MeetExistingPeers", existingPeerIds, Context.ConnectionAborted);
            await Clients.GroupExcept(PeerGroup(sessionId), new[] { Context.ConnectionId })
                .SendAsync("MeetPeerJoined", Context.ConnectionId, Context.ConnectionAborted);
        }
    } 

    public async Task RelayWebRtcSignal(int sessionId, string targetConnectionId, string signalType, object signalData)
    {
        if (string.IsNullOrWhiteSpace(targetConnectionId)
            || targetConnectionId.Length > 128
            || signalType is not ("Offer" or "Answer" or "IceCandidate"))
        {
            throw new HubException("Invalid WebRTC signal.");
        }

        await EnsurePeerAccessAsync(sessionId);

        // Reconnects and peer teardown can race with in-flight SDP/ICE messages.
        // A stale connection ID is not a meeting access failure; safely drop that signal.
        if (!PeerSessions.TryGetValue(Context.ConnectionId, out var senderSessionId)
            || senderSessionId != sessionId
            || !PeerSessions.TryGetValue(targetConnectionId, out var targetSessionId)
            || targetSessionId != sessionId)
        {
            return;
        }

        await Clients.Client(targetConnectionId).SendAsync("MeetWebRtcSignal", new
        {
            sessionId,
            senderConnectionId = Context.ConnectionId,
            signalType,
            signalData
        }, Context.ConnectionAborted);
    }

    public async Task LeavePeerRoom(int sessionId)
    {
        if (PeerSessions.TryGetValue(Context.ConnectionId, out var mappedSessionId)
            && mappedSessionId == sessionId)
        {
            PeerSessions.TryRemove(Context.ConnectionId, out _);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, PeerGroup(sessionId), Context.ConnectionAborted);
            await Clients.Group(PeerGroup(sessionId)).SendAsync("MeetPeerLeft", Context.ConnectionId, Context.ConnectionAborted);
        }
    }

    private async Task EnsurePeerAccessAsync(int sessionId)
    {
        var now = DateTime.UtcNow;
        var session = await _dbContext.MeetSessions
            .AsNoTracking()
            .Where(item => item.Id == sessionId && !item.MeetBatch.IsArchived)
            .Select(item => new
            {
                item.MeetBatchId,
                item.Status,
                item.StartedAtUtc,
                item.DurationMinutes
            })
            .SingleOrDefaultAsync(Context.ConnectionAborted);

        if (session is null
            || session.Status != "Live"
            || !session.StartedAtUtc.HasValue
            || session.StartedAtUtc.Value.AddMinutes(session.DurationMinutes) <= now)
        {
            throw new HubException("Meeting is not active.");
        }

        if (Context.User!.IsInRole("Admin") || Context.User.IsInRole("Super User"))
            return;

        var studentId = Context.UserIdentifier;
        if (string.IsNullOrWhiteSpace(studentId)
            || !Context.User.IsInRole("Student")
            || !await _dbContext.MeetBatchMembers.AsNoTracking().AnyAsync(
                member => member.MeetBatchId == session.MeetBatchId
                    && member.StudentUserId == studentId,
                Context.ConnectionAborted)
            || !await _dbContext.MeetSessionParticipants.AsNoTracking().AnyAsync(
                participant => participant.MeetSessionId == sessionId
                    && participant.StudentUserId == studentId
                    && participant.Status == "Admitted",
                Context.ConnectionAborted))
        {
            throw new HubException("You are not admitted to this meeting.");
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (PeerSessions.TryRemove(Context.ConnectionId, out var sessionId))
        {
            await Clients.Group(PeerGroup(sessionId)).SendAsync("MeetPeerLeft", Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }
}
