using System.Collections.Concurrent;
using System.Security.Claims;
using CIITStackLab.Infrastructure.Persistence;
using CIITStackLab.WebApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace CIITStackLab.WebApp.Hubs;

[Authorize]
public sealed class MeetHub : Hub
{
    private readonly ApplicationDbContext _dbContext;
    private readonly MeetIceServerService _iceServers;
    private static readonly object PeerJoinLock = new();
    private static readonly ConcurrentDictionary<string, PeerMembership> PeerSessions = new();

    private sealed record PeerMembership(int SessionId, string DisplayName);

    public MeetHub(ApplicationDbContext dbContext, MeetIceServerService iceServers)
    {
        _dbContext = dbContext;
        _iceServers = iceServers;
    }

    public static string SessionGroup(int sessionId) => $"meet-session-{sessionId}";
    public static string AdminSessionGroup(int sessionId) => $"meet-session-admins-{sessionId}";
    public static string BatchGroup(int batchId) => $"meet-batch-{batchId}";
    public const string AdminGroup = "meet-admins";
    public static string PeerGroup(int sessionId) => $"meet-session-peers-{sessionId}";
    public static string ChatGroup(int sessionId) => $"meet-session-chat-{sessionId}";
    public static string StudentSessionGroup(int sessionId, string studentId) =>
        $"meet-session-student-{sessionId}-{studentId}";

    private static readonly ConcurrentDictionary<int, ConcurrentQueue<object>> ChatHistory = new();
    private static readonly ConcurrentDictionary<string, (int SessionId, string UserId, string DisplayName)> RaisedHands = new();
    private static readonly ConcurrentDictionary<string, (int SessionId, string UserId, string DisplayName)> RaisedHandConnections = new();

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

        var isAdmin = Context.User!.IsInRole("Admin") || Context.User.IsInRole("Super User");
        var session = await _dbContext.MeetSessions
            .AsNoTracking()
            .Where(item => item.Id == sessionId && !item.MeetBatch.IsArchived)
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
                item.ChatEnabled,
                pendingCount = isAdmin ? 0 : item.Participants.Count(participant => participant.Status == "Pending"),
                participantCount = isAdmin ? 0 : item.Participants.Count(participant => participant.Status == "Admitted")
            })
            .SingleOrDefaultAsync(Context.ConnectionAborted);

        if (session is null)
        {
            // A stale room can remain open while the lifecycle worker or the
            // host completes and removes the session. Treat that as a normal
            // room shutdown instead of surfacing a SignalR invocation error.
            await Clients.Caller.SendAsync("MeetRoomUnavailable", new
            {
                sessionId,
                message = "This meeting is no longer available."
            }, Context.ConnectionAborted);
            return;
        }

        var now = DateTime.UtcNow;
        var isActive = session.Status == "Live"
            && session.StartedAtUtc is { } startedAtUtc
            && startedAtUtc <= now
            && startedAtUtc.AddMinutes(session.DurationMinutes) > now;
        var studentId = Context.UserIdentifier
            ?? Context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? Context.User.FindFirstValue("sub");

        if (!isAdmin)
        {
            if (string.IsNullOrWhiteSpace(studentId))
                throw new HubException("Your account identity could not be verified. Please sign in again.");

            if (!Context.User.IsInRole("Student"))
                throw new HubException("Your account does not have the Student role. Please contact the administrator.");

            var isBatchMember = await _dbContext.MeetBatchMembers.AsNoTracking().AnyAsync(
                member => member.MeetBatchId == session.MeetBatchId
                    && member.StudentUserId == studentId,
                Context.ConnectionAborted);

            if (!isBatchMember)
                throw new HubException("You are not enrolled in this meeting's batch.");

            // Subscribe before reading participant status so admission changes during the join can reach this tab.
            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                StudentSessionGroup(sessionId, studentId),
                Context.ConnectionAborted);
            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                SessionGroup(sessionId),
                Context.ConnectionAborted);

            var participant = await _dbContext.MeetSessionParticipants.AsNoTracking()
                .Where(item => item.MeetSessionId == sessionId && item.StudentUserId == studentId)
                .Select(item => new { item.Status, item.CanUseMicrophone, item.CanUseCamera, item.CanShareScreen })
                .SingleOrDefaultAsync(Context.ConnectionAborted);

            if (isActive && participant is null)
                throw new HubException("Your join request was not found. Please use the Join button on the student meeting page.");

            var canUseMedia = isActive && participant?.Status == "Admitted";
            var canReadPermissions = isActive && participant?.Status is "Pending" or "Admitted";
            await Clients.Caller.SendAsync("MeetMyParticipantStatus", new
            {
                sessionId,
                status = participant?.Status ?? "Left",
                sessionStatus = session.Status,
                canUseMicrophone = canReadPermissions && participant!.CanUseMicrophone,
                canUseCamera = canReadPermissions && participant!.CanUseCamera,
                canShareScreen = canReadPermissions && participant!.CanShareScreen
            }, Context.ConnectionAborted);

            await Clients.Caller.SendAsync("MeetRoomUpdated", new
            {
                sessionId,
                session.Status,
                session.WaitingRoomEnabled,
                session.ChatEnabled,
                session.pendingCount,
                session.participantCount
            }, Context.ConnectionAborted);

            if (!canUseMedia)
            {
                await LeavePeerRoom(sessionId);
                return;
            }
        }

        if (isAdmin)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, AdminSessionGroup(sessionId), Context.ConnectionAborted);

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
                sessionId = session.Id,
                batchId = session.MeetBatchId,
                session.Title,
                session.Status,
                startsAtUtc = session.StartsAtUtc,
                startedAtUtc = session.StartedAtUtc,
                endedAtUtc = session.EndedAtUtc,
                cancelledAtUtc = session.CancelledAtUtc,
                session.DurationMinutes,
                session.WaitingRoomEnabled,
                session.ChatEnabled,
                pendingCount = adminParticipants.Count(item => item.Status == "Pending"),
                participantCount = adminParticipants.Count(item => item.Status == "Admitted"),
                participants = adminParticipants
            }, Context.ConnectionAborted);
        }

        if (!isActive || !await EnsurePeerAccessAsync(sessionId))
        {
            await LeavePeerRoom(sessionId);
            return;
        }

        var joined = false;
        if (!PeerSessions.TryGetValue(Context.ConnectionId, out var membership) || membership.SessionId != sessionId)
        {
            if (membership is not null) await LeavePeerRoom(membership.SessionId);

            membership = new PeerMembership(sessionId, await GetMeetDisplayNameAsync());
            await Groups.AddToGroupAsync(Context.ConnectionId, PeerGroup(sessionId), Context.ConnectionAborted);
            joined = true;
        }

        // Only the later of two simultaneous arrivals includes the other in its
        // initial snapshot and starts their WebRTC negotiation.
        object[] existingPeers;
        lock (PeerJoinLock)
        {
            existingPeers = PeerSessions
                .Where(pair => pair.Value.SessionId == sessionId && pair.Key != Context.ConnectionId)
                .Select(pair => new { connectionId = pair.Key, displayName = pair.Value.DisplayName })
                .ToArray();
            if (joined) PeerSessions[Context.ConnectionId] = membership;
        }

        await Clients.Caller.SendAsync("MeetExistingPeers", existingPeers, Context.ConnectionAborted);
        if (joined)
        {
            await Clients.GroupExcept(PeerGroup(sessionId), new[] { Context.ConnectionId })
                .SendAsync("MeetPeerJoined", new
                {
                    connectionId = Context.ConnectionId,
                    displayName = membership.DisplayName
                }, Context.ConnectionAborted);
        }
    }

    public async Task<IReadOnlyList<MeetIceServer>> GetIceServers(int sessionId)
    {
        var endsAtUtc = await GetPeerAccessEndAsync(sessionId);
        if (!endsAtUtc.HasValue)
            throw new HubException("You are not admitted to this meeting.");

        var userId = Context.UserIdentifier
            ?? Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? Context.User?.FindFirstValue("sub");
        return await _iceServers.GetAsync(sessionId, userId, endsAtUtc.Value, Context.ConnectionAborted);
    }

    public async Task JoinChatRoom(int sessionId)
    {
        if (!await EnsurePeerAccessAsync(sessionId))
            throw new HubException("You are not admitted to this meeting.");

        await Groups.AddToGroupAsync(Context.ConnectionId, ChatGroup(sessionId), Context.ConnectionAborted);

        var chatEnabled = await _dbContext.MeetSessions.AsNoTracking()
            .Where(item => item.Id == sessionId)
            .Select(item => item.ChatEnabled)
            .SingleAsync(Context.ConnectionAborted);

        await Clients.Caller.SendAsync("MeetChatEnabledState", new { sessionId, enabled = chatEnabled }, Context.ConnectionAborted);

        if (ChatHistory.TryGetValue(sessionId, out var history))
            await Clients.Caller.SendAsync("MeetChatHistory", history.ToArray(), Context.ConnectionAborted);

        var raisedHands = RaisedHands.Values
            .Where(hand => hand.SessionId == sessionId)
            .Select(hand => new { userId = hand.UserId, displayName = hand.DisplayName })
            .DistinctBy(hand => hand.userId)
            .ToArray();
        await Clients.Caller.SendAsync("MeetHandSnapshot", new { sessionId, raisedHands }, Context.ConnectionAborted);
    }

    public async Task SendChatMessage(int sessionId, string message)
    {
        var text = message?.Trim();
        if (string.IsNullOrWhiteSpace(text) || text.Length > 1000)
            throw new HubException("Chat messages must contain 1–1000 characters.");

        if (!await EnsurePeerAccessAsync(sessionId))
            throw new HubException("You are not admitted to this meeting.");

        var chatEnabled = await _dbContext.MeetSessions.AsNoTracking()
            .Where(item => item.Id == sessionId)
            .Select(item => item.ChatEnabled)
            .SingleAsync(Context.ConnectionAborted);

        if (!chatEnabled)
            throw new HubException("The host has disabled meeting chat.");

        var displayName = await GetMeetDisplayNameAsync();
        var payload = new
        {
            sessionId,
            userId = Context.UserIdentifier,
            displayName,
            message = text,
            sentAtUtc = DateTime.UtcNow
        };

        var history = ChatHistory.GetOrAdd(sessionId, _ => new ConcurrentQueue<object>());
        history.Enqueue(payload);
        while (history.Count > 100 && history.TryDequeue(out _)) { }

        await Clients.Group(ChatGroup(sessionId)).SendAsync("MeetChatMessage", payload, Context.ConnectionAborted);
    }

    public async Task RaiseHand(int sessionId, bool raised)
    {
        if (!await EnsurePeerAccessAsync(sessionId))
            throw new HubException("You are not admitted to this meeting.");

        var userId = Context.UserIdentifier
            ?? Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? Context.User?.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(userId))
            throw new HubException("Your account identity could not be verified.");

        var displayName = await GetMeetDisplayNameAsync();
        var handKey = $"{sessionId}:{userId}";

        if (raised)
        {
            // Raised-hand state belongs to the participant/session, not a transient
            // SignalR connection. Keep it across refreshes and reconnects.
            RaisedHands[handKey] = (sessionId, userId, displayName);
            RaisedHandConnections[Context.ConnectionId] = (sessionId, userId, displayName);
        }
        else
        {
            RaisedHands.TryRemove(handKey, out _);
            foreach (var connection in RaisedHandConnections
                .Where(pair => pair.Value.SessionId == sessionId && pair.Value.UserId == userId)
                .Select(pair => pair.Key)
                .ToArray())
            {
                RaisedHandConnections.TryRemove(connection, out _);
            }
        }

        await Clients.Group(ChatGroup(sessionId)).SendAsync("MeetHandRaised", new
        {
            sessionId,
            userId,
            displayName,
            raised
        }, Context.ConnectionAborted);
    }

    private async Task<string> GetMeetDisplayNameAsync()
    {
        if (Context.User!.IsInRole("Admin") || Context.User.IsInRole("Super User"))
            return "Host";

        var userId = Context.UserIdentifier
            ?? Context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? Context.User.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(userId))
            return "Student";

        return await _dbContext.Users.AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => user.UserName ?? user.Email ?? "Student")
            .SingleOrDefaultAsync(Context.ConnectionAborted) ?? "Student";
    }

    public async Task RelayWebRtcSignal(int sessionId, string targetConnectionId, string signalType, object signalData)
    {
        if (string.IsNullOrWhiteSpace(targetConnectionId)
            || targetConnectionId.Length > 128
            || signalType is not ("Offer" or "Answer" or "IceCandidate"))
        {
            throw new HubException("Invalid WebRTC signal.");
        }

        if (!await EnsurePeerAccessAsync(sessionId)) return;

        // Reconnects and peer teardown can race with in-flight SDP/ICE messages.
        // A stale connection ID is not a meeting access failure; safely drop that signal.
        if (!PeerSessions.TryGetValue(Context.ConnectionId, out var sender)
            || sender.SessionId != sessionId
            || !PeerSessions.TryGetValue(targetConnectionId, out var target)
            || target.SessionId != sessionId)
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

    public async Task BroadcastPeerMediaState(int sessionId, string mediaKind, bool enabled)
    {
        if (mediaKind is not ("mic" or "camera" or "share"))
            throw new HubException("Invalid media state.");

        if (!await EnsurePeerAccessAsync(sessionId)) return;
        if (!PeerSessions.TryGetValue(Context.ConnectionId, out var membership)
            || membership.SessionId != sessionId)
            return;

        await Clients.GroupExcept(PeerGroup(sessionId), new[] { Context.ConnectionId })
            .SendAsync("MeetPeerMediaState", new
            {
                sessionId,
                connectionId = Context.ConnectionId,
                mediaKind,
                enabled
            }, Context.ConnectionAborted);
    }

    public async Task LeavePeerRoom(int sessionId)
    {
        if (PeerSessions.TryGetValue(Context.ConnectionId, out var membership)
            && membership.SessionId == sessionId
            && PeerSessions.TryRemove(Context.ConnectionId, out _))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, PeerGroup(sessionId), Context.ConnectionAborted);
            await Clients.Group(PeerGroup(sessionId)).SendAsync("MeetPeerLeft", Context.ConnectionId, Context.ConnectionAborted);
        }
    }

    private async Task<bool> EnsurePeerAccessAsync(int sessionId) =>
        (await GetPeerAccessEndAsync(sessionId)).HasValue;

    private async Task<DateTime?> GetPeerAccessEndAsync(int sessionId)
    {
        var now = DateTime.UtcNow;
        var user = Context.User!;
        var isAdmin = user.IsInRole("Admin") || user.IsInRole("Super User");
        var studentId = Context.UserIdentifier;
        var isStudent = !string.IsNullOrWhiteSpace(studentId) && user.IsInRole("Student");
        var session = await _dbContext.MeetSessions
            .AsNoTracking()
            .Where(item => item.Id == sessionId && !item.MeetBatch.IsArchived)
            .Select(item => new
            {
                item.Status,
                item.StartedAtUtc,
                item.DurationMinutes,
                HasAccess = isAdmin || (isStudent
                    && item.MeetBatch.Members.Any(member => member.StudentUserId == studentId)
                    && item.Participants.Any(participant => participant.StudentUserId == studentId
                        && participant.Status == "Admitted"))
            })
            .SingleOrDefaultAsync(Context.ConnectionAborted);

        if (session is null || session.Status != "Live" || !session.StartedAtUtc.HasValue)
            return null;

        var endsAtUtc = session.StartedAtUtc.Value.AddMinutes(session.DurationMinutes);
        if (endsAtUtc <= now)
            return null;

        if (!session.HasAccess)
            throw new HubException("You are not admitted to this meeting.");

        return endsAtUtc;
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // A participant refresh/reconnect must not lower their hand. The hand is
        // cleared only when RaiseHand(sessionId, false) is explicitly invoked.
        RaisedHandConnections.TryRemove(Context.ConnectionId, out _);

        if (PeerSessions.TryRemove(Context.ConnectionId, out var membership))
            await Clients.Group(PeerGroup(membership.SessionId)).SendAsync("MeetPeerLeft", Context.ConnectionId);

        await base.OnDisconnectedAsync(exception);
    }
}
