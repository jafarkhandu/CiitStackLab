namespace CIITStackLab.Application.DTOs;

public sealed class CourseEnrollmentDto
{
    public int Id { get; init; }

    public int CourseId { get; init; }

    public string Status { get; init; } = string.Empty;

    public decimal PriceAtEnrollment { get; init; }

    public DateTime RequestedAt { get; init; }

    public DateTime? ReviewedAt { get; init; }

    public bool HasAccess => string.Equals(
        Status,
        "Active",
        StringComparison.Ordinal);
}
