namespace CIITStackLab.Domain.Entities;

public sealed class PaymentTransaction
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public int CourseId { get; set; }

    public string PaymentPurpose { get; set; } = "Course";

    public int? ExamTopicId { get; set; }

    public int? ExamAttemptNumber { get; set; }

    public long? ExamAttemptId { get; set; }

    public string MerchantOrderId { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Status { get; set; } = "Created";

    public string? PhonePeOrderId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }
}
