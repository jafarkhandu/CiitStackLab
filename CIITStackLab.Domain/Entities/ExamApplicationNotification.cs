namespace CIITStackLab.Domain.Entities;

public sealed class ExamApplicationNotification
{
    public long Id { get; set; }

    // Hash of admin, student, course, topic and application-local day.
    // The compact key supports a unique SQL Server index without exceeding key-size limits.
    public string DedupeKey { get; set; } = string.Empty;

    public string AdminUserId { get; set; } = string.Empty;
    public string StudentUserId { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;

    public int CourseId { get; set; }
    public string CourseTitle { get; set; } = string.Empty;

    public int TopicId { get; set; }
    public string TopicTitle { get; set; } = string.Empty;

    public int ValidQuestionCount { get; set; }
    public int RequiredQuestionCount { get; set; } = 50;
    public string Reason { get; set; } = string.Empty;

    // This is the business day in the configured application timezone, stored as SQL date.
    public DateTime NotificationDay { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // Delivery and read state are intentionally independent.
    public bool IsDelivered { get; set; }
    public bool IsRead { get; set; }
}
