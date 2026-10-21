using System.Collections.Concurrent;
using CIITStackLab.Infrastructure.Persistence;
using CIITStackLab.WebApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace CIITStackLab.WebApp.Hubs;

[Authorize(Roles = "Admin,Super User")]
public sealed class ExamApplicationNotificationHub : Hub
{
    public const string AdminGroup = "exam-admins";

    private static readonly ConcurrentDictionary<string, int> OnlineAdmins = new();

    private readonly ApplicationDbContext _db;
    private readonly ILogger<ExamApplicationNotificationHub> _logger;

    public ExamApplicationNotificationHub(
        ApplicationDbContext db,
        ILogger<ExamApplicationNotificationHub> logger)
    {
        _db = db;
        _logger = logger;
    }

    public static bool IsAdminOnline(string adminUserId)
    {
        return !string.IsNullOrWhiteSpace(adminUserId)
            && OnlineAdmins.TryGetValue(adminUserId, out var connections)
            && connections > 0;
    }

    public override async Task OnConnectedAsync()
    {
        var adminUserId = Context.UserIdentifier;
        if (!string.IsNullOrWhiteSpace(adminUserId))
        {
            OnlineAdmins.AddOrUpdate(adminUserId, 1, (_, count) => count + 1);
            await Groups.AddToGroupAsync(Context.ConnectionId, AdminGroup, Context.ConnectionAborted);

            var pending = await _db.ExamApplicationNotifications
                .AsNoTracking()
                .Where(notification =>
                    notification.AdminUserId == adminUserId
                    && !notification.IsDelivered
                    && !notification.IsRead)
                .OrderBy(notification => notification.CreatedAtUtc)
                .Take(100)
                .ToListAsync(Context.ConnectionAborted);

            if (pending.Count > 0)
            {
                try
                {
                    await Clients.Caller.SendAsync(
                        "ExamMissedNotificationsReceived",
                        pending.Select(ExamApplicationNotificationService.ToPayload).ToArray(),
                        Context.ConnectionAborted);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    // Keep the rows undelivered so another connection can safely retry them.
                    _logger.LogWarning(
                        exception,
                        "Unable to replay pending exam notifications for administrator {AdminUserId}.",
                        adminUserId);
                }
            }
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var adminUserId = Context.UserIdentifier;
        if (!string.IsNullOrWhiteSpace(adminUserId))
        {
            OnlineAdmins.AddOrUpdate(adminUserId, 0, (_, count) => Math.Max(0, count - 1));
            if (OnlineAdmins.TryGetValue(adminUserId, out var remaining) && remaining <= 0)
            {
                OnlineAdmins.TryRemove(adminUserId, out _);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task AcknowledgeExamNotification(long notificationId)
    {
        var adminUserId = Context.UserIdentifier;
        if (string.IsNullOrWhiteSpace(adminUserId) || notificationId <= 0)
        {
            return;
        }

        var notification = await _db.ExamApplicationNotifications
            .SingleOrDefaultAsync(
                item => item.Id == notificationId && item.AdminUserId == adminUserId,
                Context.ConnectionAborted);

        if (notification is null || notification.IsDelivered)
        {
            return;
        }

        notification.IsDelivered = true;
        await _db.SaveChangesAsync(Context.ConnectionAborted);
    }
}
