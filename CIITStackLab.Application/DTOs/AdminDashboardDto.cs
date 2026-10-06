namespace CIITStackLab.Application.DTOs;

public sealed class AdminDashboardDto
{
    public int CourseCount { get; init; }
    public int TopicCount { get; init; }
    public int ContentCount { get; init; }
    public int McqCount { get; init; }
    public int StudentCount { get; init; }
    public IReadOnlyList<AdminDashboardCourseDto> RecentCourses { get; init; } =
        Array.Empty<AdminDashboardCourseDto>();
}

public sealed class AdminDashboardCourseDto
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public DateTime? UpdatedAt { get; init; }
}
