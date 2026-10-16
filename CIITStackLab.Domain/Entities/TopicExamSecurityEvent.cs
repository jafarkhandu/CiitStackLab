namespace CIITStackLab.Domain.Entities;

public sealed class TopicExamSecurityEvent
{
    public long Id { get; set; }
    public long ExamAttemptId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string? Details { get; set; }
    public DateTime OccurredAtUtc { get; set; }
}
