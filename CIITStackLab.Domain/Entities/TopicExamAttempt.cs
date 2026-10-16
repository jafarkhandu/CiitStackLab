namespace CIITStackLab.Domain.Entities;

public sealed class TopicExamAttempt
{
    public long Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int CourseId { get; set; }
    public int TopicId { get; set; }
    public DateTime ScheduledAtUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    public DateTime? DeadlineAtUtc { get; set; }
    public string Status { get; set; } = "Scheduled";
    public string QuestionIdsJson { get; set; } = "[]";
    public string OptionOrderJson { get; set; } = "{}";
    public string AnswersJson { get; set; } = "{}";
    public string? DeviceTokenHash { get; set; }
    public int WarningCount { get; set; }
    public int Score { get; set; }
    public int TotalQuestions { get; set; }
    public bool Passed { get; set; }
    public string? TerminationReason { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
