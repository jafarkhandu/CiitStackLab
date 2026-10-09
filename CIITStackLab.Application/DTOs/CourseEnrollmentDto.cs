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

public sealed class CourseEnrollmentRequestResultDto
{
    public bool Succeeded { get; init; }

    public bool HasAccess { get; init; }

    public bool IsFreeCourse { get; init; }

    public string Status { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public static CourseEnrollmentRequestResultDto Failure(string message)
    {
        return new CourseEnrollmentRequestResultDto
        {
            Succeeded = false,
            Message = message
        };
    }
}

public sealed class PendingCourseEnrollmentDto
{
    public int Id { get; init; }

    public int CourseId { get; init; }

    public string CourseTitle { get; init; } = string.Empty;

    public string UserId { get; init; } = string.Empty;

    public string StudentUserName { get; init; } = string.Empty;

    public string StudentEmail { get; init; } = string.Empty;

    public decimal PriceAtEnrollment { get; init; }

    public DateTime RequestedAt { get; init; }

    public string Status { get; init; } = string.Empty;
}
