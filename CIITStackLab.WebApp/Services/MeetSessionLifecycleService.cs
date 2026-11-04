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
    private readonly MeetNotificationService _notifications;

    public MeetSessionLifecycleService(
        IServiceScopeFactory scopeFactory,
        ILogger<MeetSessionLifecycleService> logger,
        MeetNotificationService notifications)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _notifications = notifications;
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

        var reminderSessions = await dbContext.MeetSessions
            .Include(session => session.MeetBatch)
            .Where(session => session.Status == "Scheduled"
                && !session.MeetBatch.IsArchived
                && session.ReminderSentAtUtc == null
                && session.StartsAtUtc > now
                && session.StartsAtUtc <= now.AddMinutes(10))
            .ToListAsync(cancellationToken);

        foreach (var session in reminderSessions)
        {
            var minutesUntilStart = Math.Max(1, (int)Math.Ceiling((session.StartsAtUtc - now).TotalMinutes));
            var message = $"Meeting \"{session.Title}\" starts in {minutesUntilStart} minute(s).";
            await _notifications.NotifyBatchAsync(session.MeetBatchId, "Meet reminder", message, "warning", cancellationToken);
            await _notifications.NotifyAdminsAsync("Upcoming Meet", $"{message} Batch: {session.MeetBatch.Name}", cancellationToken);
            session.ReminderSentAtUtc = now;
        }

        if (reminderSessions.Count > 0)
            await dbContext.SaveChangesAsync(cancellationToken);

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
            {
                await realtime.PublishSessionUpdateAsync(session.Id, cancellationToken);
                dbContext.MeetSessionParticipants.RemoveRange(session.Participants);
                dbContext.MeetSessions.Remove(session);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
