using System.ComponentModel.DataAnnotations;

namespace CIITStackLab.Application.DTOs;

public sealed class AdminNoteDto
{
    public int Id { get; init; }
    public int CourseId { get; init; }
    public string CourseTitle { get; init; } = string.Empty;
    public int TopicId { get; init; }
    public string TopicTitle { get; init; } = string.Empty;
    public string PageId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string TitleWithNumber { get; init; } = string.Empty;
    public string HtmlContent { get; init; } = string.Empty;
    public int SortOrder { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public DateTime? CreatedAt { get; init; }
}

public sealed class AdminNoteInputDto
{
    public int Id { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Select a course.")]
    public int CourseId { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Select a topic.")]
    public int TopicId { get; set; }
    [Required, StringLength(50)]
    public string PageId { get; set; } = string.Empty;
    [Required, StringLength(100)]
    public string Title { get; set; } = string.Empty;
    [Required, StringLength(150)]
    public string TitleWithNumber { get; set; } = string.Empty;
    [Required(ErrorMessage = "Chapter content is required.")]
    public string HtmlContent { get; set; } = string.Empty;
    [Range(1, int.MaxValue, ErrorMessage = "Sort order must be 1 or greater.")]
    public int SortOrder { get; set; } = 1;
}
public sealed class NoteCourseTopicOptionDto
{
    public int CourseId { get; init; }
    public string CourseTitle { get; init; } = string.Empty;
    public int TopicId { get; init; }
    public string TopicTitle { get; init; } = string.Empty;
}
public sealed class CourseNotesDto
{
    public int CourseId { get; init; }
    public string CourseTitle { get; init; } = string.Empty;
    public int TopicId { get; init; }
    public string TopicTitle { get; init; } = string.Empty;
    public IReadOnlyList<NoteChapterDto> Chapters { get; init; } = Array.Empty<NoteChapterDto>();
    public string? CurrentPageId { get; init; }
    public NoteChapterDto? CurrentChapter { get; init; }
}
public sealed class NoteChapterDto
{
    public int Id { get; init; }
    public string PageId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string TitleWithNumber { get; init; } = string.Empty;
    public string HtmlContent { get; init; } = string.Empty;
    public int SortOrder { get; init; }
}