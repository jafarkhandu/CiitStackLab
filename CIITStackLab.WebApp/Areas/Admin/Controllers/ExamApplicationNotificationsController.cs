using System.Security.Claims;
using CIITStackLab.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CIITStackLab.WebApp.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,Super User")]
[Route("Admin/ExamApplicationNotifications")]
public sealed class ExamApplicationNotificationsController : Controller
{
    private readonly ApplicationDbContext _db;

    public ExamApplicationNotificationsController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet("Pending")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> Pending(CancellationToken cancellationToken)
    {
        var adminUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(adminUserId))
        {
            return Unauthorized();
        }

        // Read notifications are cleared from the inbox view but retained in storage
        // so the unique daily deduplication key continues to prevent repeated alerts.
        var notifications = await _db.ExamApplicationNotifications
            .AsNoTracking()
            .Where(notification => notification.AdminUserId == adminUserId && !notification.IsRead)
            .OrderByDescending(notification => notification.CreatedAtUtc)
            .Take(100)
            .Select(notification => new
            {
                notificationId = notification.Id,
                courseId = notification.CourseId,
                courseTitle = notification.CourseTitle,
                topicId = notification.TopicId,
                topicTitle = notification.TopicTitle,
                studentUserId = notification.StudentUserId,
                studentName = notification.StudentName,
                validQuestionCount = notification.ValidQuestionCount,
                requiredQuestionCount = notification.RequiredQuestionCount,
                reason = notification.Reason,
                occurredAtUtc = notification.CreatedAtUtc,
                isRead = notification.IsRead,
                isDelivered = notification.IsDelivered
            })
            .ToListAsync(cancellationToken);

        return Json(new
        {
            success = true,
            notifications,
            unreadCount = notifications.Count
        });
    }

    [HttpPost("{notificationId:long}/MarkRead")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead(long notificationId, CancellationToken cancellationToken)
    {
        var adminUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(adminUserId))
        {
            return Unauthorized();
        }

        var notification = await _db.ExamApplicationNotifications
            .SingleOrDefaultAsync(
                item => item.Id == notificationId && item.AdminUserId == adminUserId,
                cancellationToken);

        if (notification is null)
        {
            return NotFound();
        }

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return Json(new { success = true, notificationId });
    }

    [HttpPost("MarkAllRead")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        var adminUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(adminUserId))
        {
            return Unauthorized();
        }

        var changed = await _db.ExamApplicationNotifications
            .Where(notification => notification.AdminUserId == adminUserId && !notification.IsRead)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(notification => notification.IsRead, true),
                cancellationToken);

        return Json(new { success = true, changed });
    }
}
