using CIITStackLab.Application.DTOs;

namespace CIITStackLab.Application.Interfaces;

public interface ITopicExaminationService
{
    Task<IReadOnlyList<TopicExamSummaryDto>> GetStudentExamsAsync(string userId, CancellationToken cancellationToken = default);
    Task<(bool Succeeded, string? Error, TopicExamSummaryDto? Exam)> ScheduleAsync(string userId, int courseId, int topicId, DateTime scheduledAtUtc, CancellationToken cancellationToken = default);
    Task<(bool Succeeded, string? Error, TopicExamSessionDto? Session)> OpenPreExamAsync(string userId, long attemptId, string deviceToken, CancellationToken cancellationToken = default);
    Task<(bool Succeeded, string? Error, TopicExamSessionDto? Session)> StartAsync(string userId, long attemptId, string deviceToken, CancellationToken cancellationToken = default);
    Task<(bool Succeeded, string? Error, TopicExamSummaryDto? Result)> SaveAnswersAsync(string userId, long attemptId, IReadOnlyList<TopicExamAnswerInput> answers, CancellationToken cancellationToken = default);
    Task<bool> RecordSecurityEventAsync(string userId, long attemptId, string eventType, string? details, string deviceToken, CancellationToken cancellationToken = default);
    Task<bool> HasPassedTopicAsync(string userId, int courseId, int topicId, CancellationToken cancellationToken = default);
}
