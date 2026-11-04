using CIITStackLab.Infrastructure.Persistence;
using CIITStackLab.WebApp.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace CIITStackLab.WebApp.Services;

public sealed class MeetRealtimeService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IHubContext<MeetHub> _hub;
    private readonly ILogger<MeetRealtimeService> _logger;

    public MeetRealtimeService(
        ApplicationDbContext dbContext,
        IHubContext<MeetHub> hub,
        ILogger<MeetRealtimeService> logger)
    {
        _dbContext = dbContext;
        _hub = hub;
        _logger = logger;
    }

    public async Task PublishSessionUpdateAsync(int sessionId, CancellationToken cancellationToken = default)
    {
        var session = await _dbContext.MeetSessions
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
                item.ChatEnabled,
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (session is null)
            return;

        var participants = await (
            from participant in _dbContext.MeetSessionParticipants.AsNoTracking()
            join user in _dbContext.Users.AsNoTracking()
                on participant.StudentUserId equals user.Id
            where participant.MeetSessionId == session.Id
            orderby participant.RequestedAtUtc
            select new
            {
                participant.Id,
                participant.StudentUserId,
                StudentName = user.UserName ?? user.Email ?? "Student",
                participant.Status,
                participant.CanUseMicrophone,
                participant.CanUseCamera,
                participant.CanShareScreen
            }).ToListAsync(cancellationToken);

        var adminSnapshot = new
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
            pendingCount = participants.Count(item => item.Status == "Pending"),
            participantCount = participants.Count(item => item.Status == "Admitted"),
            participants = participants.Select(item => new
            {
                item.Id,
                item.StudentName,
                item.Status,
                item.CanUseMicrophone,
                item.CanUseCamera,
                item.CanShareScreen
            }).ToList()
        };

        // Students receive room state and aggregate counts only. Participant names,
        // statuses, and host-controlled permissions are restricted to admin connections.
        var studentSnapshot = new
        {
            adminSnapshot.sessionId,
            adminSnapshot.batchId,
            adminSnapshot.Title,
            adminSnapshot.Status,
            adminSnapshot.startsAtUtc,
            adminSnapshot.startedAtUtc,
            adminSnapshot.endedAtUtc,
            adminSnapshot.cancelledAtUtc,
            adminSnapshot.DurationMinutes,
            adminSnapshot.WaitingRoomEnabled,
            adminSnapshot.ChatEnabled,
            adminSnapshot.pendingCount,
            adminSnapshot.participantCount,
            participants = Array.Empty<object>()
        };

        try
        {
            foreach (var participant in participants)
            {
                var status = new
                {
                    sessionId = session.Id,
                    status = participant.Status,
                    sessionStatus = session.Status,
                    canUseMicrophone = participant.CanUseMicrophone,
                    canUseCamera = participant.CanUseCamera,
                    canShareScreen = participant.CanShareScreen
                };

                // The per-student room group also covers waiting-room tabs and
                // avoids relying on a single UserIdentifier claim mapping.
                await Task.WhenAll(
                    _hub.Clients.User(participant.StudentUserId)
                        .SendAsync("MeetMyParticipantStatus", status, cancellationToken),
                    _hub.Clients.Group(MeetHub.StudentSessionGroup(session.Id, participant.StudentUserId))
                        .SendAsync("MeetMyParticipantStatus", status, cancellationToken));
            }

            await _hub.Clients.Group(MeetHub.SessionGroup(session.Id))
                .SendAsync("MeetRoomUpdated", studentSnapshot, cancellationToken);

            await _hub.Clients.Group(MeetHub.BatchGroup(session.MeetBatchId))
                .SendAsync("MeetBatchUpdated", studentSnapshot, cancellationToken);

            await _hub.Clients.Group(MeetHub.AdminGroup)
                .SendAsync("MeetAdminUpdated", adminSnapshot, cancellationToken);

            await _hub.Clients.Group(MeetHub.AdminSessionGroup(session.Id))
                .SendAsync("MeetRoomUpdated", adminSnapshot, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Live Meet update delivery failed for session {MeetSessionId}. The saved meeting state remains available on the next page load.",
                session.Id);
        }
    }

    public async Task PublishChatEnabledAsync(
        int sessionId,
        CancellationToken cancellationToken = default)
    {
        var enabled = await _dbContext.MeetSessions
            .AsNoTracking()
            .Where(item => item.Id == sessionId)
            .Select(item => item.ChatEnabled)
            .SingleAsync(cancellationToken);

        // Broadcast chat availability to every active meeting connection, including
        // students still in the waiting room who have not joined the chat group.
        await Task.WhenAll(
            _hub.Clients.Group(MeetHub.SessionGroup(sessionId))
                .SendAsync("MeetChatEnabledState", new { sessionId, enabled }, cancellationToken),
            _hub.Clients.Group(MeetHub.AdminSessionGroup(sessionId))
                .SendAsync("MeetChatEnabledState", new { sessionId, enabled }, cancellationToken),
            _hub.Clients.Group(MeetHub.ChatGroup(sessionId))
                .SendAsync("MeetChatEnabledState", new { sessionId, enabled }, cancellationToken));
    }
}
