namespace CIITStackLab.Domain.Entities;

public sealed class CourseEnrollment
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public int CourseId { get; set; }

    public string Status { get; set; } = CourseEnrollmentStatuses.PendingApproval;

    public decimal PriceAtEnrollment { get; set; }

    public DateTime RequestedAt { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public string? ReviewedByUserId { get; set; }
}

public static class CourseEnrollmentStatuses
{
    public const string PendingApproval = "PendingApproval";
    public const string Active = "Active";
    public const string Rejected = "Rejected";
}
