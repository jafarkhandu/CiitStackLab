using System.ComponentModel.DataAnnotations;

namespace CIITStackLab.Application.DTOs;

public sealed class AdminPracticeProgramDto
{
    public int Id { get; init; }
    public int? ContentId { get; init; }
    public string ContentTitle { get; init; } = string.Empty;
    public string TopicTitle { get; init; } = string.Empty;
    public string QuestionTitle { get; init; } = string.Empty;
    public string QuestionDescription { get; init; } = string.Empty;
    public IReadOnlyList<AdminPracticeProgramAnswerDto> Answers { get; set; } = Array.Empty<AdminPracticeProgramAnswerDto>();
    public DateTime? CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public sealed class AdminArchivedPracticeProgramDto
{
    public int Id { get; init; }
    public string ContentTitle { get; init; } = string.Empty;
    public string TopicTitle { get; init; } = string.Empty;
    public string QuestionTitle { get; init; } = string.Empty;
    public DateTime? DeletedAt { get; init; }
}

public sealed class AdminPracticeProgramAnswerDto
{
    public int Id { get; init; }
    public int? ProgramQuestionId { get; init; }
    public string Answer { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public DateTime? CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public sealed class AdminArchivedPracticeProgramAnswerDto
{
    public int Id { get; init; }
    public int? ProgramQuestionId { get; init; }
    public string QuestionTitle { get; init; } = string.Empty;
    public string Answer { get; init; } = string.Empty;
    public DateTime? DeletedAt { get; init; }
}

public sealed class AdminPracticeProgramIndexDto
{
    public IReadOnlyList<AdminPracticeProgramDto> ActivePrograms { get; init; } = Array.Empty<AdminPracticeProgramDto>();
    public IReadOnlyList<AdminArchivedPracticeProgramDto> ArchivedPrograms { get; init; } = Array.Empty<AdminArchivedPracticeProgramDto>();
    public IReadOnlyList<AdminArchivedPracticeProgramAnswerDto> ArchivedAnswers { get; init; } = Array.Empty<AdminArchivedPracticeProgramAnswerDto>();
}

public sealed class AdminPracticeProgramInputDto
{
    [Range(1, int.MaxValue, ErrorMessage = "Please select content.")]
    public int ContentId { get; set; }

    [Required(ErrorMessage = "Question title is required.")]
    public string QuestionTitle { get; set; } = string.Empty;

    [Required(ErrorMessage = "Question description is required.")]
    public string QuestionDescription { get; set; } = string.Empty;
}

public sealed class AdminPracticeProgramAnswerInputDto
{
    [Range(1, int.MaxValue, ErrorMessage = "Please select a practice program.")]
    public int ProgramQuestionId { get; set; }

    [Required(ErrorMessage = "Answer is required.")]
    public string Answer { get; set; } = string.Empty;

    [Required(ErrorMessage = "Solution description is required.")]
    public string Description { get; set; } = string.Empty;
}

public sealed class AdminPracticeProgramContentLookupDto
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string TopicTitle { get; init; } = string.Empty;
}

public sealed class AdminPracticeProgramLookupDto
{
    public int Id { get; init; }
    public string QuestionTitle { get; init; } = string.Empty;
}
