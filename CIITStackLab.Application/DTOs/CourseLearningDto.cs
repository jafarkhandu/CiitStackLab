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
    public CourseLearningProgressDto? Progress { get; set; }
}

public sealed class CourseLearningTopicDto
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public int? DurationMinutes { get; init; }
    public IReadOnlyList<CourseLearningContentDto> Contents { get; init; } = Array.Empty<CourseLearningContentDto>();
    public IReadOnlyList<CourseLearningChapterDto> Chapters { get; init; } = Array.Empty<CourseLearningChapterDto>();
}

public sealed class CourseLearningChapterDto
{
    public int Id { get; init; }
    public string ChapterId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public int SortOrder { get; init; }
    public IReadOnlyList<CourseLearningContentDto> Contents { get; init; } = Array.Empty<CourseLearningContentDto>();
}

public sealed class CourseLearningContentDto
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Slides { get; init; }
    public string? VideoName { get; init; }
    public IReadOnlyList<ContentQuestionDto> Questions { get; init; } = Array.Empty<ContentQuestionDto>();
}

public sealed class CourseLearningProgressDto
{
    public int TotalLessons { get; init; }

    public int CompletedLessons { get; init; }

    public int Percentage { get; init; }

    public int? LastAccessedContentId { get; init; }

    public IReadOnlyCollection<int> CompletedContentIds { get; init; } = Array.Empty<int>();
}
