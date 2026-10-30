using CIITStackLab.Infrastructure.Persistence;
using CIITStackLab.WebApp.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace CIITStackLab.WebApp.Services;

public sealed class MeetRealtimeService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IHubContext<MeetHub> _hub;

    public MeetRealtimeService(ApplicationDbContext dbContext, IHubContext<MeetHub> hub)
    {
        _dbContext = dbContext;
        _hub = hub;
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
                item.RecordingEnabled,
                item.RecordingStatus,
                item.RecordingRetentionUntilUtc
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
                StudentName = user.UserName ?? user.Email ?? "Student",
                participant.Status,
                participant.CanUseMicrophone,
                participant.CanUseCamera,
                participant.CanShareScreen
            }).ToListAsync(cancellationToken);

        var snapshot = new
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
            session.RecordingEnabled,
            session.RecordingStatus,
            recordingRetentionUntilUtc = session.RecordingRetentionUntilUtc,
            pendingCount = participants.Count(item => item.Status == "Pending"),
            participantCount = participants.Count(item => item.Status == "Admitted"),
            participants
        };

        await _hub.Clients.Group(MeetHub.SessionGroup(session.Id))
            .SendAsync("MeetRoomUpdated", snapshot, cancellationToken);

        await _hub.Clients.Group(MeetHub.BatchGroup(session.MeetBatchId))
            .SendAsync("MeetBatchUpdated", snapshot, cancellationToken);

        await _hub.Clients.Group(MeetHub.AdminGroup)
            .SendAsync("MeetAdminUpdated", snapshot, cancellationToken);
    }
}
