using System.ComponentModel.DataAnnotations;

namespace CIITStackLab.Application.DTOs;

public sealed class AdminContentDto
{
    public int Id { get; init; }
    public int CourseId { get; init; }
    public string CourseTitle { get; init; } = string.Empty;
    public int TopicId { get; init; }
    public int? NoteId { get; init; }
    public string NoteChapterId { get; init; } = string.Empty;
    public string NoteTitle { get; init; } = string.Empty;
    public string TopicTitle { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Slides { get; init; }
    public string? VideoName { get; init; }
    public int Flag { get; init; }
    public DateTime? CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public DateTime? DeletedAt { get; init; }
    public DateTime? RestoredAt { get; init; }
}

public sealed class AdminArchivedContentDto
{
    public int Id { get; init; }
    public int CourseId { get; init; }
    public string CourseTitle { get; init; } = string.Empty;
    public int TopicId { get; init; }
    public string TopicTitle { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public DateTime? DeletedAt { get; init; }
}

public sealed class AdminContentIndexDto
{
    public IReadOnlyList<AdminContentDto> ActiveContents { get; init; } = Array.Empty<AdminContentDto>();
    public IReadOnlyList<AdminArchivedContentDto> ArchivedContents { get; init; } = Array.Empty<AdminArchivedContentDto>();
}

public sealed class AdminContentInputDto
{
    public int Id { get; set; }

    // Retained for existing data compatibility; Course is not selected in the new Content UI.
    public int CourseId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Please select a topic.")]
    public int TopicId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Please select a note chapter.")]
    public int NoteId { get; set; }

    [Required(ErrorMessage = "Content name is required.")]
    [StringLength(100, ErrorMessage = "Content name cannot exceed 100 characters.")]
    public string Title { get; set; } = string.Empty;

    public string? Slides { get; set; }

    [StringLength(100, ErrorMessage = "Video name cannot exceed 100 characters.")]
    public string? VideoName { get; set; }
}

public sealed class AdminLookupDto
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
}

public sealed class AdminNoteLookupDto
{
    public int Id { get; init; }
    public string ChapterId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public int SortOrder { get; init; }
}
