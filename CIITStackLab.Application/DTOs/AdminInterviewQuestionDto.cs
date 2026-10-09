using System.ComponentModel.DataAnnotations;

namespace CIITStackLab.Application.DTOs;

public sealed class AdminInterviewQuestionDto
{
    public int Id { get; init; }
    public int? ContentId { get; init; }
    public string ContentTitle { get; init; } = string.Empty;
    public string TopicTitle { get; init; } = string.Empty;
    public string Question { get; init; } = string.Empty;
    public string Answer { get; init; } = string.Empty;
    public DateTime? CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public sealed class AdminArchivedInterviewQuestionDto
{
    public int Id { get; init; }
    public string ContentTitle { get; init; } = string.Empty;
    public string TopicTitle { get; init; } = string.Empty;
    public string Question { get; init; } = string.Empty;
    public DateTime? DeletedAt { get; init; }
}

public sealed class AdminInterviewQuestionIndexDto
{
    public IReadOnlyList<AdminInterviewQuestionDto> ActiveQuestions { get; init; } = Array.Empty<AdminInterviewQuestionDto>();
    public IReadOnlyList<AdminArchivedInterviewQuestionDto> ArchivedQuestions { get; init; } = Array.Empty<AdminArchivedInterviewQuestionDto>();
}

public sealed class AdminInterviewQuestionInputDto
{
    [Range(1, int.MaxValue, ErrorMessage = "Please select content.")]
    public int ContentId { get; set; }

    [Required(ErrorMessage = "Question is required.")]
    public string Question { get; set; } = string.Empty;

    [Required(ErrorMessage = "Answer is required.")]
    public string Answer { get; set; } = string.Empty;
}

public sealed class AdminInterviewQuestionContentLookupDto
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string TopicTitle { get; init; } = string.Empty;
}
