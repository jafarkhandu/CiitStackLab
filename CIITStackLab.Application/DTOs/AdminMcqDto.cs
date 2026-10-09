using System.ComponentModel.DataAnnotations;

namespace CIITStackLab.Application.DTOs;

public sealed class AdminMcqDto
{
    public int Id { get; init; }
    public int? ContentId { get; init; }
    public string ContentTitle { get; init; } = string.Empty;
    public string TopicTitle { get; init; } = string.Empty;
    public string Question { get; init; } = string.Empty;
    public string Option1 { get; init; } = string.Empty;
    public string Option2 { get; init; } = string.Empty;
    public string Option3 { get; init; } = string.Empty;
    public string Option4 { get; init; } = string.Empty;
    public int CorrectOptionNumber { get; init; }
    public DateTime? CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public sealed class AdminArchivedMcqDto
{
    public int Id { get; init; }
    public string ContentTitle { get; init; } = string.Empty;
    public string TopicTitle { get; init; } = string.Empty;
    public string Question { get; init; } = string.Empty;
    public DateTime? DeletedAt { get; init; }
}

public sealed class AdminMcqIndexDto
{
    public IReadOnlyList<AdminMcqDto> ActiveMcqs { get; init; } = Array.Empty<AdminMcqDto>();
    public IReadOnlyList<AdminArchivedMcqDto> ArchivedMcqs { get; init; } = Array.Empty<AdminArchivedMcqDto>();
}

public sealed class AdminMcqInputDto
{
    [Range(1, int.MaxValue, ErrorMessage = "Please select content.")]
    public int ContentId { get; set; }

    [Required(ErrorMessage = "Question is required.")]
    public string Question { get; set; } = string.Empty;

    [Required(ErrorMessage = "Option 1 is required.")]
    public string Option1 { get; set; } = string.Empty;

    [Required(ErrorMessage = "Option 2 is required.")]
    public string Option2 { get; set; } = string.Empty;

    [Required(ErrorMessage = "Option 3 is required.")]
    public string Option3 { get; set; } = string.Empty;

    [Required(ErrorMessage = "Option 4 is required.")]
    public string Option4 { get; set; } = string.Empty;

    [Range(1, 4, ErrorMessage = "Correct option must be between 1 and 4.")]
    public int CorrectOptionNumber { get; set; }
}

public sealed class AdminMcqContentLookupDto
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string TopicTitle { get; init; } = string.Empty;
}
