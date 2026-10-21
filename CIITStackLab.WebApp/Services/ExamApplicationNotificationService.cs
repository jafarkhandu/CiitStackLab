using System.Security.Cryptography;
using System.Text;
using CIITStackLab.Domain.Entities;
using CIITStackLab.Infrastructure.Persistence;
using CIITStackLab.WebApp.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace CIITStackLab.WebApp.Services;

public sealed class ExamApplicationNotificationService
{
    private const int RequiredQuestionCount = 50;

    private readonly ApplicationDbContext _db;
    private readonly IHubContext<ExamApplicationNotificationHub> _hub;
    private readonly ILogger<ExamApplicationNotificationService> _logger;

    public ExamApplicationNotificationService(
        ApplicationDbContext db,
        IHubContext<ExamApplicationNotificationHub> hub,
        ILogger<ExamApplicationNotificationService> logger)
    {
        _db = db;
        _hub = hub;
        _logger = logger;
    }

    public async Task CreateMissingQuestionsNotificationsAsync(
        string studentUserId,
        string studentName,
        int courseId,
        int topicId,
        int validQuestionCount,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(studentUserId)
            || courseId <= 0
            || topicId <= 0
            || string.IsNullOrWhiteSpace(reason))
        {
            return;
        }

        var topicContext = await (
            from module in _db.CourseModules.AsNoTracking()
            join course in _db.Courses.AsNoTracking()
                on module.CourseId equals course.Id
            join topic in _db.Topics.AsNoTracking()
                on module.TopicId equals topic.Id
            where module.CourseId == courseId
                && module.TopicId == topicId
                && module.Flag == 0
                && course.Flag == 0
                && topic.Flag == 0
            select new
            {
                CourseTitle = course.Title,
                TopicTitle = topic.Title
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (topicContext is null)
        {
            return;
        }

        var adminIds = await (
            from userRole in _db.UserRoles.AsNoTracking()
            join role in _db.Roles.AsNoTracking()
                on userRole.RoleId equals role.Id
            where role.Name == "Admin" || role.Name == "Super User"
            select userRole.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (adminIds.Count == 0)
        {
            _logger.LogWarning(
                "No Admin or Super User accounts are configured to receive missing-exam-question alerts.");
            return;
        }

        var nowUtc = DateTime.UtcNow;
        var notificationDay = GetApplicationDay(nowUtc);
        var displayName = string.IsNullOrWhiteSpace(studentName)
            ? studentUserId
            : studentName.Trim();

        foreach (var adminId in adminIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var dedupeKey = CreateDedupeKey(
                adminId,
                studentUserId,
                courseId,
                topicId,
                notificationDay);

            var alreadyExists = await _db.ExamApplicationNotifications.AsNoTracking()
                .AnyAsync(
                    notification => notification.DedupeKey == dedupeKey,
                    cancellationToken);

            if (alreadyExists)
            {
                continue;
            }

            var notification = new ExamApplicationNotification
            {
                DedupeKey = dedupeKey,
                AdminUserId = adminId,
                StudentUserId = studentUserId,
                StudentName = displayName.Length <= 256 ? displayName : displayName[..256],
                CourseId = courseId,
                CourseTitle = Limit(topicContext.CourseTitle, 200),
                TopicId = topicId,
                TopicTitle = Limit(topicContext.TopicTitle, 200),
                ValidQuestionCount = Math.Max(0, validQuestionCount),
                RequiredQuestionCount = RequiredQuestionCount,
                Reason = Limit(reason, 1000),
                NotificationDay = notificationDay,
                CreatedAtUtc = nowUtc,
                IsDelivered = false,
                IsRead = false
            };

            _db.ExamApplicationNotifications.Add(notification);

            try
            {
                // The unique index is the final concurrency guard when two requests arrive together.
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                _db.Entry(notification).State = EntityState.Detached;

                if (await _db.ExamApplicationNotifications.AsNoTracking()
                    .AnyAsync(item => item.DedupeKey == dedupeKey, cancellationToken))
                {
                    continue;
                }

                throw;
            }

            if (!ExamApplicationNotificationHub.IsAdminOnline(adminId))
            {
                // Persisted as undelivered; the SignalR hub replays it on the next admin connection.
                continue;
            }

            try
            {
                await _hub.Clients.User(adminId).SendAsync(
                    "ExamQuestionsUnavailable",
                    ToPayload(notification),
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                // A lost live connection must not lose the persisted notification.
                _logger.LogWarning(
                    exception,
                    "Live delivery failed for exam-question alert {NotificationId}; it remains pending.",
                    notification.Id);
            }
        }
    }

    public static object ToPayload(ExamApplicationNotification notification)
    {
        return new
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
        };
    }

    private static string CreateDedupeKey(
        string adminUserId,
        string studentUserId,
        int courseId,
        int topicId,
        DateTime notificationDay)
    {
        var key = string.Join(
            "|",
            adminUserId,
            studentUserId,
            courseId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            topicId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            notificationDay.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
    }

    private static DateTime GetApplicationDay(DateTime utcNow)
    {
        TimeZoneInfo timeZone;
        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            try
            {
                timeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
            }
            catch (TimeZoneNotFoundException)
            {
                timeZone = TimeZoneInfo.Utc;
            }
        }

        return TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utcNow, DateTimeKind.Utc),
            timeZone).Date;
    }

    private static string Limit(string? value, int maximumLength)
    {
        var text = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        return text.Length <= maximumLength ? text : text[..maximumLength];
    }
}
