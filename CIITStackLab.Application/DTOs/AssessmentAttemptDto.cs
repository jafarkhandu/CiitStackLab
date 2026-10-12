using System.ComponentModel.DataAnnotations;

namespace CIITStackLab.Application.DTOs;

public sealed class AssessmentAnswerDto
{
    [Range(1, int.MaxValue)]
    public int QuestionId { get; set; }

    public int? SelectedOptionNumber { get; set; }
}

public sealed class AssessmentSubmissionOutcomeDto
{
    public bool Succeeded { get; init; }

    public string? ErrorMessage { get; init; }

    public AssessmentAttemptResultDto? Result { get; init; }

    public static AssessmentSubmissionOutcomeDto Success(
        AssessmentAttemptResultDto result)
    {
        return new AssessmentSubmissionOutcomeDto
        {
            Succeeded = true,
            Result = result
        };
    }

    public static AssessmentSubmissionOutcomeDto Failure(string message)
    {
        return new AssessmentSubmissionOutcomeDto
        {
            Succeeded = false,
            ErrorMessage = message
        };
    }
}

public sealed class AssessmentAttemptResultDto
{
    public int Id { get; init; }

    public int CourseId { get; init; }

    public int ContentId { get; init; }

    public int Score { get; init; }

    public int TotalQuestions { get; init; }

    public int Percentage { get; init; }

    public DateTime SubmittedAt { get; init; }

    public IReadOnlyList<AssessmentQuestionResultDto> Questions { get; init; }
        = Array.Empty<AssessmentQuestionResultDto>();
}

public sealed class AssessmentQuestionResultDto
{
    public int QuestionId { get; init; }

    public string Question { get; init; } = string.Empty;

    public IReadOnlyList<string?> Options { get; init; } = Array.Empty<string?>();

    public int? SelectedOptionNumber { get; init; }

    public int CorrectOptionNumber { get; init; }

    public bool IsCorrect { get; init; }
}
