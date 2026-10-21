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
    public IReadOnlyList<AdminDashboardCourseDto> ActiveCourses { get; init; } =
        Array.Empty<AdminDashboardCourseDto>();
    public IReadOnlyList<AdminDashboardTopicDto> ActiveTopics { get; init; } =
        Array.Empty<AdminDashboardTopicDto>();
    public IReadOnlyList<AdminDashboardContentDto> LearningContent { get; init; } =
        Array.Empty<AdminDashboardContentDto>();
    public IReadOnlyList<AdminDashboardMcqDto> ActiveMcqs { get; init; } =
        Array.Empty<AdminDashboardMcqDto>();
    public IReadOnlyList<AdminDashboardStudentDto> Students { get; init; } =
        Array.Empty<AdminDashboardStudentDto>();
}

public sealed class AdminDashboardCourseDto
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public DateTime? UpdatedAt { get; init; }
}

public sealed class AdminDashboardTopicDto
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public DateTime? UpdatedAt { get; init; }
}

public sealed class AdminDashboardContentDto
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public int? TopicId { get; init; }
    public string TopicTitle { get; init; } = string.Empty;
    public DateTime? UpdatedAt { get; init; }
}

public sealed class AdminDashboardMcqDto
{
    public int Id { get; init; }
    public string Question { get; init; } = string.Empty;
    public int? ContentId { get; init; }
    public int? TopicId { get; init; }
}

public sealed class AdminDashboardStudentDto
{
    public string Id { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
}
