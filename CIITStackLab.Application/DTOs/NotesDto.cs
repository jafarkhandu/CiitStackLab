using System.ComponentModel.DataAnnotations;

namespace CIITStackLab.Application.DTOs;

public sealed class AdminNoteDto
{
    public int Id { get; init; }
    public int TopicId { get; init; }
    public string TopicTitle { get; init; } = string.Empty;
    public string ChapterId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string HtmlContent { get; init; } = string.Empty;
    public int SortOrder { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public DateTime? CreatedAt { get; init; }
}

public sealed class AdminNoteInputDto
{
    public int Id { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Select a topic.")]
    public int TopicId { get; set; }

    [Required, StringLength(100)]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Chapter content is required.")]
    public string HtmlContent { get; set; } = string.Empty;

}

public sealed class NoteTopicOptionDto
{
    public int TopicId { get; init; }
    public string TopicTitle { get; init; } = string.Empty;
}

public sealed class TopicNotesDto
{
    public int TopicId { get; init; }
    public string TopicTitle { get; init; } = string.Empty;
    public IReadOnlyList<NoteChapterDto> Chapters { get; init; } = Array.Empty<NoteChapterDto>();
    public string? CurrentChapterId { get; init; }
    public NoteChapterDto? CurrentChapter { get; init; }
}

public sealed class NoteChapterDto
{
    public int Id { get; init; }
    public string ChapterId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string HtmlContent { get; init; } = string.Empty;
    public int SortOrder { get; init; }
    public IReadOnlyList<NoteMediaDto> Media { get; init; } = Array.Empty<NoteMediaDto>();
}

public sealed class NoteMediaDto
{
    public int ContentId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Slides { get; init; }
    public string? VideoName { get; init; }
}


/// <summary>
/// A selectable unified content record. NoteId is retained as a legacy DTO field
/// and carries the content_id value; it does not reference tbltraining_notes.
/// </summary>
public sealed class AdminAssessmentChapterLookupDto
{
    public int NoteId { get; init; }

    public int TopicId { get; init; }

    public string TopicTitle { get; init; } = string.Empty;

    public string ChapterId { get; init; } = string.Empty;

    public string ChapterTitle { get; init; } = string.Empty;
}
