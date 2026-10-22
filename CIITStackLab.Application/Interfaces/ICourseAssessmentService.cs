using CIITStackLab.Application.DTOs;

namespace CIITStackLab.Application.Interfaces;

public interface ICourseAssessmentService
{
    Task<AssessmentSubmissionOutcomeDto> SubmitAsync(
        string userId,
        int courseId,
        int? contentId,
        int? topicId,
        IReadOnlyList<AssessmentAnswerDto> answers,
        CancellationToken cancellationToken = default);

    Task<AssessmentAttemptResultDto?> GetAttemptAsync(
        string userId,
        int courseId,
        int attemptId,
        CancellationToken cancellationToken = default);

    Task<AssessmentAttemptResultDto?> GetExistingTopicAttemptAsync(
        string userId,
        int courseId,
        int topicId,
        CancellationToken cancellationToken = default);
}
