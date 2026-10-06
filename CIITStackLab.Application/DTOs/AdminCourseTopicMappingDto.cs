namespace CIITStackLab.Application.DTOs;

public sealed class AdminCourseTopicMappingPageDto
{
    public IReadOnlyList<AdminMappingCourseDto> Courses { get; init; } =
        Array.Empty<AdminMappingCourseDto>();

    public int? SelectedCourseId { get; init; }

    public string? SelectedCourseName { get; init; }

    public IReadOnlyList<AdminMappingTopicDto> Topics { get; init; } =
        Array.Empty<AdminMappingTopicDto>();

    public int AssignedTopicCount { get; init; }
}

public sealed class AdminMappingCourseDto
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
}

public sealed class AdminMappingTopicDto
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public bool IsAssigned { get; init; }
}
