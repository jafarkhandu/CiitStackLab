using System.ComponentModel.DataAnnotations;

namespace CIITStackLab.Application.DTOs;

public sealed class AdminNotesDto
{
    public int Id { get; init; }
    public int CourseId { get; init; }
    public string CourseTitle { get; init; } = string.Empty;
    public int TopicId { get; init; }
    public string TopicTitle { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? HtmlContent { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public DateTime? CreatedAt { get; init; }
}

public sealed class AdminNotesInputDto
{
    public int Id { get; set; }

    [Required(ErrorMessage = "HTML content is required.")]
    public string HtmlContent { get; set; } = string.Empty;
}
