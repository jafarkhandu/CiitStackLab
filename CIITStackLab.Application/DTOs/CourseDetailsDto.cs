namespace CIITStackLab.Application.DTOs;

public sealed class CourseDetailsDto
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string ShortDescription { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string ImageUrl { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Level { get; init; } = string.Empty;
    public int DurationHours { get; init; }
    public IReadOnlyList<CourseTopicDto> Topics { get; init; } = Array.Empty<CourseTopicDto>();
}

public sealed class CourseTopicDto
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public bool HasNotes { get; init; }
    public IReadOnlyList<CourseContentDto> Contents { get; init; } = Array.Empty<CourseContentDto>();
}

public sealed class CourseContentDto
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Slides { get; init; }
    public string? VideoName { get; init; }
    public IReadOnlyList<ContentQuestionDto> Questions { get; init; } = Array.Empty<ContentQuestionDto>();
}

public sealed class ContentQuestionDto
{
    public int Id { get; init; }
    public string Question { get; init; } = string.Empty;
    public IReadOnlyList<string?> Options { get; init; } = Array.Empty<string?>();
}
