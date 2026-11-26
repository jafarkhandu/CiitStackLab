using System.Text.Json;
using CIITStackLab.Application.DTOs;
using CIITStackLab.Application.Interfaces;
using CIITStackLab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CIITStackLab.Infrastructure.Services;

/// <summary>
/// Applies the examination-application rules used by the Hatim flow while
/// delegating the actual exam engine to the existing implementation.
/// This keeps the current exam engine isolated and avoids copying payment code.
/// </summary>
public sealed class HatimExamApplicationService : ITopicExaminationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly TopicExaminationService _inner;
    private readonly ApplicationDbContext _db;

    public HatimExamApplicationService(
        TopicExaminationService inner,
        ApplicationDbContext db)
    {
        _inner = inner;
        _db = db;
    }

    public async Task<IReadOnlyList<TopicExamSummaryDto>> GetStudentExamsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var summaries = await _inner.GetStudentExamsAsync(userId, cancellationToken);
        if (summaries.Count == 0)
            return summaries;

        var attemptIds = summaries.Select(x => x.AttemptId).ToList();
        var answerSnapshots = await _db.TopicExamAttempts.AsNoTracking()
            .Where(x => attemptIds.Contains(x.Id))
            .Select(x => new { x.Id, x.AnswersJson })
            .ToListAsync(cancellationToken);

        var answersByAttempt = answerSnapshots.ToDictionary(
            x => x.Id,
            x => GetAnsweredQuestionsCount(x.AnswersJson));

        return summaries.Select(summary => new TopicExamSummaryDto
        {
            AttemptId = summary.AttemptId,
            CourseId = summary.CourseId,
            TopicId = summary.TopicId,
            TopicTitle = summary.TopicTitle,
            ScheduledAtUtc = summary.ScheduledAtUtc,
            StartedAtUtc = summary.StartedAtUtc,
            DeadlineAtUtc = summary.DeadlineAtUtc,
            SubmittedAtUtc = summary.SubmittedAtUtc,
            Status = summary.Status,
            Score = summary.Score,
            TotalQuestions = summary.TotalQuestions,
            AnsweredQuestionsCount = answersByAttempt.GetValueOrDefault(summary.AttemptId),
            Passed = summary.Passed,
            WarningCount = summary.WarningCount,
            TerminationReason = summary.TerminationReason
        }).ToList();
    }

    public Task<(bool Succeeded, string? Error, TopicExamSummaryDto? Exam)> ScheduleAsync(
        string userId,
        int courseId,
        int topicId,
        DateTime scheduledAtUtc,
        CancellationToken cancellationToken = default,
        bool startNow = false) =>
        _inner.ScheduleAsync(
            userId,
            courseId,
            topicId,
            scheduledAtUtc,
            cancellationToken,
            startNow);
    public Task<(bool IsAvailable, int ValidQuestionCount, string? Error)> CheckQuestionAvailabilityAsync(
        string userId,
        int courseId,
        int topicId,
        CancellationToken cancellationToken = default) =>
        _inner.CheckQuestionAvailabilityAsync(
            userId, courseId, topicId, cancellationToken);

    public Task<(bool Succeeded, string? Error, TopicExamSessionDto? Session)> OpenPreExamAsync(
        string userId, long attemptId, string deviceToken, CancellationToken cancellationToken = default) =>
        _inner.OpenPreExamAsync(userId, attemptId, deviceToken, cancellationToken);

    public Task<(bool Succeeded, string? Error, TopicExamSessionDto? Session)> StartAsync(
        string userId, long attemptId, string deviceToken, CancellationToken cancellationToken = default) =>
        _inner.StartAsync(userId, attemptId, deviceToken, cancellationToken);

    public Task<(bool Succeeded, string? Error, TopicExamSummaryDto? Result)> SaveAnswersAsync(
        string userId,
        long attemptId,
        IReadOnlyList<TopicExamAnswerInput> answers,
        string deviceToken,
        CancellationToken cancellationToken = default,
        bool finalSubmission = false) =>
        _inner.SaveAnswersAsync(
            userId, attemptId, answers, deviceToken, cancellationToken, finalSubmission);

    public Task<bool> RecordSecurityEventAsync(
        string userId,
        long attemptId,
        string eventType,
        string? details,
        string deviceToken,
        CancellationToken cancellationToken = default) =>
        _inner.RecordSecurityEventAsync(
            userId, attemptId, eventType, details, deviceToken, cancellationToken);

    public Task<bool> HasPassedTopicAsync(
        string userId,
        int courseId,
        int topicId,
        CancellationToken cancellationToken = default) =>
        _inner.HasPassedTopicAsync(userId, courseId, topicId, cancellationToken);

    public Task<int> FinalizeExpiredAttemptsAsync(CancellationToken cancellationToken = default) =>
        _inner.FinalizeExpiredAttemptsAsync(cancellationToken);

    private static int GetAnsweredQuestionsCount(string? answersJson)
    {
        try
        {
            var answers = JsonSerializer.Deserialize<Dictionary<int, int?>>(
                answersJson ?? "{}",
                JsonOptions);
            return answers?.Count(pair => pair.Value.HasValue) ?? 0;
        }
        catch (JsonException)
        {
            return 0;
        }
    }
}
