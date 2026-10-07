namespace CIITStackLab.Application.DTOs;

public sealed class CourseLearningDto
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string ShortDescription { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string ImageUrl { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Level { get; init; } = string.Empty;
    public int DurationHours { get; init; }
    public decimal TotalPrice { get; init; }
    public int TopicCount { get; init; }
    public int ContentCount { get; init; }
    public IReadOnlyList<CourseLearningTopicDto> Topics { get; init; } = Array.Empty<CourseLearningTopicDto>();
    public int? CurrentContentId { get; init; }
    public CourseLearningContentDto? CurrentContent { get; init; }
}

public sealed class CourseLearningTopicDto
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public int? DurationMinutes { get; init; }
    public IReadOnlyList<CourseLearningContentDto> Contents { get; init; } = Array.Empty<CourseLearningContentDto>();
}

public sealed class CourseLearningContentDto
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Slides { get; init; }
    public string? VideoName { get; init; }
    public string? HtmlContent { get; init; }
    public IReadOnlyList<ContentQuestionDto> Questions { get; init; } = Array.Empty<ContentQuestionDto>();
}
