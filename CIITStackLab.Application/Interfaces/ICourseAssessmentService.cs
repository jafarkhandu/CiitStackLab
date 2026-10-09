using CIITStackLab.Application.DTOs;

namespace CIITStackLab.Application.Interfaces;

public interface ICourseAssessmentService
{
    Task<AssessmentSubmissionOutcomeDto> SubmitAsync(
        string userId,
        int courseId,
        int contentId,
        IReadOnlyList<AssessmentAnswerDto> answers,
        CancellationToken cancellationToken = default);

    Task<AssessmentAttemptResultDto?> GetAttemptAsync(
        string userId,
        int courseId,
        int attemptId,
        CancellationToken cancellationToken = default);
}
