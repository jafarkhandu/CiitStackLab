using CIITStackLab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CIITStackLab.WebApp.Services;

public sealed class MeetSessionLifecycleService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MeetSessionLifecycleService> _logger;

    public MeetSessionLifecycleService(
        IServiceScopeFactory scopeFactory,
        ILogger<MeetSessionLifecycleService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await CompleteExpiredSessionsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to complete expired Meet sessions.");
            }
        }
    }

    private async Task CompleteExpiredSessionsAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = DateTime.UtcNow;

        var expiredSessions = await dbContext.MeetSessions
            .Include(session => session.Participants)
            .Where(session => session.Status == "Live"
                && session.StartedAtUtc.HasValue
                && session.StartedAtUtc.Value.AddMinutes(session.DurationMinutes) <= now)
            .ToListAsync(cancellationToken);

        foreach (var session in expiredSessions)
        {
            var endedAt = session.StartedAtUtc!.Value.AddMinutes(session.DurationMinutes);
            session.Status = "Completed";
            session.EndedAtUtc = endedAt;

            if (session.RecordingStatus == "Recording")
            {
                session.RecordingStatus = "Stopped";
                session.RecordingEndedAtUtc = endedAt;
                session.RecordingRetentionUntilUtc = endedAt.AddDays(90);
            }

            foreach (var participant in session.Participants.Where(
                participant => participant.Status is "Pending" or "Admitted"))
            {
                participant.Status = "Left";
                participant.LeftAtUtc = endedAt;
            }
        }

        if (expiredSessions.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            var realtime = scope.ServiceProvider.GetRequiredService<MeetRealtimeService>();
            foreach (var session in expiredSessions)
                await realtime.PublishSessionUpdateAsync(session.Id, cancellationToken);
        }
    }
}
