using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CIITStackLab.Application.DTOs;
using CIITStackLab.Application.Interfaces;
using CIITStackLab.Domain.Entities;
using CIITStackLab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CIITStackLab.Infrastructure.Services;

public sealed class TopicExaminationService : ITopicExaminationService
{
    private const int QuestionCount = 50;
    private const int PassingScore = 35;
    private const int ExamDurationMinutes = 60;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ApplicationDbContext _db;
    private readonly CoursePurchaseService _coursePurchaseService;

    public TopicExaminationService(
        ApplicationDbContext db,
        CoursePurchaseService coursePurchaseService)
    {
        _db = db;
        _coursePurchaseService = coursePurchaseService;
    }

    public async Task<IReadOnlyList<TopicExamSummaryDto>> GetStudentExamsAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) return Array.Empty<TopicExamSummaryDto>();
        return await _db.TopicExamAttempts.AsNoTracking()
            .Where(x => x.UserId == userId)
            .Join(_db.Topics.AsNoTracking(), x => x.TopicId, t => t.Id,
                (x, t) => new TopicExamSummaryDto
                {
                    AttemptId = x.Id, CourseId = x.CourseId, TopicId = x.TopicId, TopicTitle = t.Title,
                    ScheduledAtUtc = x.ScheduledAtUtc, StartedAtUtc = x.StartedAtUtc,
                    DeadlineAtUtc = x.DeadlineAtUtc, SubmittedAtUtc = x.SubmittedAtUtc,
                    Status = x.Status, Score = x.Score, TotalQuestions = x.TotalQuestions,
                    Passed = x.Passed, WarningCount = x.WarningCount, TerminationReason = x.TerminationReason
                })
            .OrderByDescending(x => x.ScheduledAtUtc).ToListAsync(cancellationToken);
    }

    public async Task<(bool Succeeded, string? Error, TopicExamSummaryDto? Exam)> ScheduleAsync(
        string userId, int courseId, int topicId, DateTime scheduledAtUtc, CancellationToken cancellationToken = default, bool startNow = false)
    {
        if (string.IsNullOrWhiteSpace(userId) || courseId <= 0 || topicId <= 0)
            return (false, "Invalid exam request.", null);
        scheduledAtUtc = DateTime.SpecifyKind(scheduledAtUtc, DateTimeKind.Utc);
        var now = DateTime.UtcNow;
        if (!startNow && scheduledAtUtc < now.AddMinutes(30))
            return (false, "Schedule the exam at least 30 minutes in advance.", null);
        if (startNow)
            scheduledAtUtc = now;

        var topicExists = await (
            from cm in _db.CourseModules.AsNoTracking()
            join course in _db.Courses.AsNoTracking() on cm.CourseId equals course.Id
            join topic in _db.Topics.AsNoTracking() on cm.TopicId equals topic.Id
            where cm.CourseId == courseId && cm.TopicId == topicId
                && cm.Flag == 0 && course.Flag == 0 && topic.Flag == 0
            select topic.Id).AnyAsync(cancellationToken);
        if (!topicExists) return (false, "The selected topic is not active in this course.", null);

        // Use the same access rule as the learning and payment flows.
        // Topic-based pricing is authoritative; the legacy Courses.FeesAmount field
        // can disagree with the actual course price and incorrectly block exam access.
        var hasAccess = await _coursePurchaseService.CanAccessCourseAsync(
            userId,
            courseId,
            cancellationToken);
        if (!hasAccess) return (false, "You do not have access to this course.", null);

        var hasEverPassedTopic = await _db.TopicExamAttempts.AsNoTracking().AnyAsync(x =>
            x.UserId == userId && x.CourseId == courseId && x.TopicId == topicId
            && x.Passed && x.Status == "Passed", cancellationToken);

        var previousAttemptCount = await _db.TopicExamAttempts.CountAsync(x =>
            x.UserId == userId && x.CourseId == courseId && x.TopicId == topicId,
            cancellationToken);
        var nextAttemptNumber = previousAttemptCount + 1;
        var paymentRequired = nextAttemptNumber > 2 || hasEverPassedTopic;
        CIITStackLab.Domain.Entities.PaymentTransaction? retakePayment = null;
        if (paymentRequired)
        {
            retakePayment = await _db.PaymentTransactions.SingleOrDefaultAsync(payment =>
                payment.UserId == userId
                && payment.CourseId == courseId
                && payment.PaymentPurpose == "ExamRetake"
                && payment.ExamTopicId == topicId
                && payment.ExamAttemptNumber == nextAttemptNumber
                && payment.Status == "Completed"
                && payment.ExamAttemptId == null,
                cancellationToken);

            if (retakePayment is null)
            {
                var baseFee = Math.Max(1, nextAttemptNumber - 2) * 100m;
                var totalFee = baseFee + Math.Round(baseFee * 0.18m, 2, MidpointRounding.AwayFromZero);
                return (false,
                    $"Attempt #{nextAttemptNumber} requires payment of ₹{totalFee:0.00} including 18% GST before it can start.",
                    null);
            }
        }

        var activeAttempt = await _db.TopicExamAttempts.AnyAsync(x =>
            x.UserId == userId && x.CourseId == courseId && x.TopicId == topicId
            && (x.Status == "Scheduled" || x.Status == "Ready" || x.Status == "InProgress"), cancellationToken);
        if (activeAttempt) return (false, "An exam is already scheduled or active for this topic.", null);

        var topicNoteIds = await _db.TrainingNotes.AsNoTracking()
            .Where(n => n.TopicId == topicId && n.Flag == 0).Select(n => n.Id).ToListAsync(cancellationToken);
        var topicContentIds = await _db.Lessons.AsNoTracking()
            .Where(l => l.Flag == 0 && (l.TopicId == topicId || (l.NoteId.HasValue && topicNoteIds.Contains(l.NoteId.Value))))
            .Select(l => l.Id).Distinct().ToListAsync(cancellationToken);
        var completedContentIds = topicContentIds.Count == 0
            ? new List<int>()
            : await _db.StudentLessonProgress.AsNoTracking()
                .Where(p => p.UserId == userId && p.CourseId == courseId && p.IsCompleted && topicContentIds.Contains(p.ContentId))
                .Select(p => p.ContentId).Distinct().ToListAsync(cancellationToken);
        // Keep the server-side gate consistent with the learning page: a topic with no
        // lesson rows is treated as complete there, so it must not be rejected here
        // with a contradictory "no active learning content" error.
        if (topicContentIds.Count > 0 && completedContentIds.Count != topicContentIds.Count)
            return (false, "Complete all lessons in this topic before scheduling its exam.", null);

        var questionAvailability = await GetQuestionAvailabilityDataAsync(
            userId,
            courseId,
            topicId,
            cancellationToken);
        if (questionAvailability.Error is not null)
        {
            return (false, questionAvailability.Error, null);
        }

        var unseenIds = questionAvailability.UnseenIds;
        var usedIds = questionAvailability.UsedIds;

        var shuffledUnseen = unseenIds
            .OrderBy(_ => RandomNumberGenerator.GetInt32(int.MaxValue))
            .ToList();
        var unseenToUse = Math.Min(QuestionCount, shuffledUnseen.Count);
        var repeatCount = QuestionCount - unseenToUse;
        var repeatedToUse = usedIds
            .OrderBy(_ => RandomNumberGenerator.GetInt32(int.MaxValue))
            .Take(repeatCount)
            .ToList();
        var chosenIds = shuffledUnseen.Take(unseenToUse)
            .Concat(repeatedToUse)
            .OrderBy(_ => RandomNumberGenerator.GetInt32(int.MaxValue))
            .ToList();

        if (chosenIds.Count < QuestionCount)
        {
            return (false,
                $"This topic currently has only {chosenIds.Count} eligible questions available across fresh and previous attempts. Add more valid questions before scheduling the re-exam.",
                null);
        }

        var optionOrders = new Dictionary<int, List<int>>();
        foreach (var questionId in chosenIds)
            optionOrders[questionId] = new[] { 1, 2, 3, 4 }.OrderBy(_ => RandomNumberGenerator.GetInt32(int.MaxValue)).ToList();
        var attempt = new TopicExamAttempt
        {
            UserId = userId, CourseId = courseId, TopicId = topicId,
            ScheduledAtUtc = scheduledAtUtc, Status = "Scheduled",
            QuestionIdsJson = JsonSerializer.Serialize(chosenIds, JsonOptions),
            OptionOrderJson = JsonSerializer.Serialize(optionOrders, JsonOptions),
            AnswersJson = "{}", TotalQuestions = QuestionCount, CreatedAtUtc = now
        };
        if (retakePayment is null)
        {
            _db.TopicExamAttempts.Add(attempt);
            await _db.SaveChangesAsync(cancellationToken);
        }
        else
        {
            // Create the paid attempt and consume its payment atomically so a
            // database failure cannot leave a paid attempt partially linked.
            await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
            _db.TopicExamAttempts.Add(attempt);
            await _db.SaveChangesAsync(cancellationToken);

            retakePayment.ExamAttemptId = attempt.Id;
            retakePayment.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        return (true, null, await MapSummaryAsync(attempt, cancellationToken));
    }

    public async Task<(bool IsAvailable, int ValidQuestionCount, string? Error)> CheckQuestionAvailabilityAsync(
        string userId,
        int courseId,
        int topicId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || courseId <= 0 || topicId <= 0)
        {
            return (false, 0, "Invalid exam request.");
        }

        var availability = await GetQuestionAvailabilityDataAsync(
            userId,
            courseId,
            topicId,
            cancellationToken);

        return (
            availability.Error is null,
            availability.EligibleIds.Count,
            availability.Error);
    }

    public async Task<(bool Succeeded, string? Error, TopicExamSessionDto? Session)> OpenPreExamAsync(
        string userId, long attemptId, string deviceToken, CancellationToken cancellationToken = default)
    {
        var attempt = await FindOwnedAttemptAsync(userId, attemptId, cancellationToken);
        if (attempt is null) return (false, "Exam not found.", null);
        var now = DateTime.UtcNow;
        if (now < attempt.ScheduledAtUtc.AddMinutes(-30))
            return (false, "The pre-exam instructions become available 30 minutes before the scheduled exam.", null);
        var examEndUtc = attempt.ScheduledAtUtc.AddMinutes(ExamDurationMinutes);
        if (now >= examEndUtc && attempt.Status is "Scheduled" or "Ready" or "InProgress")
        {
            await FinalizeAsync(attempt, "TimedOut", now, cancellationToken);
            return (false, "Time's up. The exam window has closed.", null);
        }
        if (attempt.Status is "Submitted" or "Terminated" or "Passed" or "Failed" or "TimedOut")
            return (false, "This exam attempt is already closed.", null);
        if (!await ClaimDeviceAsync(attempt, deviceToken, cancellationToken))
            return (false, "This exam attempt is locked to the first device used.", null);
        if (attempt.VerificationStartedAtUtc is null)
            attempt.VerificationStartedAtUtc = now;
        if (attempt.Status == "Scheduled") attempt.Status = "Ready";
        await _db.SaveChangesAsync(cancellationToken);
        return (true, null, await BuildSessionAsync(attempt, includeAnswers: false, includeQuestions: false, cancellationToken));
    }

    public async Task<(bool Succeeded, string? Error, TopicExamSessionDto? Session)> StartAsync(
        string userId, long attemptId, string deviceToken, CancellationToken cancellationToken = default)
    {
        var attempt = await FindOwnedAttemptAsync(userId, attemptId, cancellationToken);
        if (attempt is null) return (false, "Exam not found.", null);
        if (!await DeviceMatchesAsync(attempt, deviceToken))
            return (false, "This exam attempt is locked to its original device.", null);
        var now = DateTime.UtcNow;
        var examEndUtc = attempt.ScheduledAtUtc.AddMinutes(ExamDurationMinutes);
        if (now >= examEndUtc)
        {
            if (attempt.Status is "Scheduled" or "Ready" or "InProgress")
                await FinalizeAsync(attempt, "TimedOut", now, cancellationToken);
            return (false, "Time's up. The exam window has closed.", null);
        }
        if (now < attempt.ScheduledAtUtc)
            return (false, "The exam will start at the scheduled time. The countdown page is not the exam.", null);
        if (!attempt.VerificationStartedAtUtc.HasValue
            || now < attempt.VerificationStartedAtUtc.Value.AddSeconds(30))
            return (false, "Complete the full 30-second pre-exam verification before starting.", null);
        if (attempt.Status == "InProgress")
        {
            if (attempt.DeadlineAtUtc.HasValue && now >= attempt.DeadlineAtUtc.Value)
            {
                await FinalizeAsync(attempt, "TimedOut", now, cancellationToken);
                return (false, "Time's up. The exam window has closed.", null);
            }

            return (false, "This exam attempt is already in progress. Reloading or reopening it is not allowed.", null);
        }
        if (attempt.Status != "Ready")
            return (false, "Open the pre-exam instructions first.", null);

        attempt.StartedAtUtc = now;
        attempt.DeadlineAtUtc = examEndUtc;
        attempt.Status = "InProgress";
        await _db.SaveChangesAsync(cancellationToken);
        return (true, null, await BuildSessionAsync(attempt, true, true, cancellationToken));
    }

    public async Task<(bool Succeeded, string? Error, TopicExamSummaryDto? Result)> SaveAnswersAsync(
        string userId, long attemptId, IReadOnlyList<TopicExamAnswerInput> answers, string deviceToken,
        CancellationToken cancellationToken = default, bool finalSubmission = false)
    {
        var attempt = await FindOwnedAttemptAsync(userId, attemptId, cancellationToken);
        if (attempt is null) return (false, "Exam not found.", null);
        if (attempt.Status != "InProgress")
        {
            var terminationDetail = string.IsNullOrWhiteSpace(attempt.TerminationReason)
                ? string.Empty
                : $" Termination reason: {attempt.TerminationReason}.";
            return (false,
                $"This exam attempt is closed (status: {attempt.Status}).{terminationDetail} A closed attempt cannot accept more answers.",
                await MapSummaryAsync(attempt, cancellationToken));
        }
        if (attempt.DeadlineAtUtc is null)
            return (false, "This exam has no recorded deadline, so the server cannot safely accept answers.", await MapSummaryAsync(attempt, cancellationToken));

        var now = DateTime.UtcNow;
        var deadlinePassed = now >= attempt.DeadlineAtUtc.Value;
        if (!await DeviceMatchesAsync(attempt, deviceToken))
            return (false, "Device verification failed for this exam attempt. Reopen the exam from its original device and session.", await MapSummaryAsync(attempt, cancellationToken));
        if (deadlinePassed && !finalSubmission)
        {
            await FinalizeAsync(attempt, "TimedOut", now, cancellationToken);
            return (false, "The exam deadline has passed. This attempt has been closed.", await MapSummaryAsync(attempt, cancellationToken));
        }

        var questionIds = DeserializeIds(attempt.QuestionIdsJson);
        var submitted = answers ?? Array.Empty<TopicExamAnswerInput>();
        if (submitted.Any(a => !questionIds.Contains(a.QuestionId)
            || (a.SelectedOptionNumber.HasValue && a.SelectedOptionNumber.Value is < 1 or > 4)))
            return (false, "Submitted answers contain invalid question or option identifiers.", null);

        var answerMap = JsonSerializer.Deserialize<Dictionary<int, int?>>(attempt.AnswersJson, JsonOptions) ?? new();
        foreach (var answer in submitted.GroupBy(a => a.QuestionId).Select(group => group.Last()))
        {
            if (answer.SelectedOptionNumber.HasValue || !answerMap.ContainsKey(answer.QuestionId))
                answerMap[answer.QuestionId] = answer.SelectedOptionNumber;
        }
        if (finalSubmission && !deadlinePassed
            && questionIds.Any(questionId => !answerMap.TryGetValue(questionId, out var selected) || !selected.HasValue))
        {
            return (false,
                "Answer every question before submitting the examination. If time runs out, the exam will submit automatically with the answers received so far.",
                await MapSummaryAsync(attempt, cancellationToken));
        }

        attempt.AnswersJson = JsonSerializer.Serialize(answerMap, JsonOptions);
        if (deadlinePassed)
            await FinalizeAsync(attempt, "TimedOut", now, cancellationToken);
        else if (finalSubmission)
            await FinalizeAsync(attempt, "Submitted", now, cancellationToken);
        else
            await _db.SaveChangesAsync(cancellationToken);
        return (true, null, await MapSummaryAsync(attempt, cancellationToken));
    }

    public async Task<bool> RecordSecurityEventAsync(
        string userId, long attemptId, string eventType, string? details, string deviceToken, CancellationToken cancellationToken = default)
    {
        var attempt = await FindOwnedAttemptAsync(userId, attemptId, cancellationToken);
        if (attempt is null || attempt.Status != "InProgress" || !await DeviceMatchesAsync(attempt, deviceToken))
            return false;
        var normalized = (eventType ?? string.Empty).Trim();
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "TabSwitch", "WindowBlur", "PageExit", "Copy", "Paste", "Cut", "ScreenshotShortcut",
            "RecordingShortcut", "CameraLost", "MultipleFaces", "FaceMissing", "SuspiciousActivity",
            "FullscreenExit", "NetworkDisconnected"
        };
        if (!allowed.Contains(normalized)) return false;

        _db.TopicExamSecurityEvents.Add(new TopicExamSecurityEvent
        {
            ExamAttemptId = attempt.Id, EventType = normalized,
            Details = string.IsNullOrWhiteSpace(details) ? null : details[..Math.Min(details.Length, 1000)],
            OccurredAtUtc = DateTime.UtcNow
        });

        if (normalized.Equals("TabSwitch", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("WindowBlur", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("PageExit", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("FullscreenExit", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("ScreenshotShortcut", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("RecordingShortcut", StringComparison.OrdinalIgnoreCase))
        {
            attempt.TerminationReason = normalized;
            await FinalizeAsync(attempt, "Terminated", DateTime.UtcNow, cancellationToken);
        }
        else if (normalized.Equals("SuspiciousActivity", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("MultipleFaces", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("FaceMissing", StringComparison.OrdinalIgnoreCase))
        {
            attempt.WarningCount++;
            if (attempt.WarningCount >= 5)
            {
                attempt.TerminationReason = "Exceeded five proctoring warnings";
                await FinalizeAsync(attempt, "Terminated", DateTime.UtcNow, cancellationToken);
            }
            else
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
        }
        else
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        return true;
    }

    public async Task<int> FinalizeExpiredAttemptsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var expired = await _db.TopicExamAttempts
            .Where(x => (x.Status == "InProgress" && x.DeadlineAtUtc.HasValue && x.DeadlineAtUtc <= now)
                || ((x.Status == "Scheduled" || x.Status == "Ready")
                    && !x.StartedAtUtc.HasValue
                    && x.ScheduledAtUtc.AddMinutes(ExamDurationMinutes) <= now))
            .ToListAsync(cancellationToken);
        foreach (var attempt in expired)
            await FinalizeAsync(attempt, "TimedOut", now, cancellationToken);

        foreach (var attempt in expired)
        {
            if (attempt.Status == "InProgress")
            {
                await FinalizeAsync(attempt, "TimedOut", now, cancellationToken);
            }
            else
            {
                attempt.Status = "TimedOut";
                attempt.Passed = false;
                attempt.Score = 0;
                attempt.TotalQuestions = QuestionCount;
                attempt.SubmittedAtUtc = now;
                attempt.TerminationReason = "Exam start window missed";
            }
        }
        if (expired.Count > 0)
            await _db.SaveChangesAsync(cancellationToken);
        return expired.Count;
    }

    public async Task<bool> HasPassedTopicAsync(string userId, int courseId, int topicId, CancellationToken cancellationToken = default)
    {
        var latestAttempt = await _db.TopicExamAttempts.AsNoTracking()
            .Where(x => x.UserId == userId && x.CourseId == courseId && x.TopicId == topicId)
            .OrderByDescending(x => x.ScheduledAtUtc)
            .ThenByDescending(x => x.Id)
            .Select(x => new { x.Status, x.Passed })
            .FirstOrDefaultAsync(cancellationToken);

        if (latestAttempt is null)
            return false;

        if (latestAttempt.Status == "Passed" && latestAttempt.Passed)
            return true;

        if (latestAttempt.Status is "Scheduled" or "Ready" or "InProgress")
        {
            return await _db.TopicExamAttempts.AsNoTracking().AnyAsync(x =>
                x.UserId == userId && x.CourseId == courseId && x.TopicId == topicId
                && x.Passed && x.Status == "Passed", cancellationToken);
        }

        return false;
    }

    public async Task<int?> GetCurrentTopicIdAsync(string userId, int courseId, CancellationToken cancellationToken = default)
    {
        var topicIds = await _db.CourseModules.AsNoTracking()
            .Where(cm => cm.CourseId == courseId && cm.Flag == 0 && cm.Topic.Flag == 0 && cm.Course.Flag == 0)
            // Keep exam progression aligned with the course-topic mapping order shown in the learning path.
            .OrderBy(cm => cm.Id)
            .Select(cm => cm.TopicId)
            .Distinct()
            .ToListAsync(cancellationToken);
        foreach (var topicId in topicIds)
        {
            if (!await HasPassedTopicAsync(userId, courseId, topicId, cancellationToken))
                return topicId;
        }
        return topicIds.LastOrDefault() is var last && last > 0 ? last : null;
    }

    private async Task FinalizeAsync(TopicExamAttempt attempt, string finalStatus, DateTime submittedAtUtc, CancellationToken cancellationToken)
    {
        var ids = DeserializeIds(attempt.QuestionIdsJson);
        var answers = JsonSerializer.Deserialize<Dictionary<int, int?>>(attempt.AnswersJson, JsonOptions) ?? new();
        var questions = await _db.TopicExamQuestions.AsNoTracking()
            .Where(q => ids.Contains(q.Id)).ToDictionaryAsync(q => q.Id, cancellationToken);
        var optionOrders = DeserializeOptionOrders(attempt.OptionOrderJson);
        var score = 0;
        foreach (var id in ids)
        {
            if (!answers.TryGetValue(id, out var selected) || !selected.HasValue || !questions.TryGetValue(id, out var q)) continue;
            if (optionOrders.TryGetValue(id, out var order) && selected.Value >= 1 && selected.Value <= order.Count
                && q.CorrectOptionNumber == order[selected.Value - 1]) score++;
        }
        attempt.Score = score;
        attempt.TotalQuestions = ids.Count;
        attempt.Passed = finalStatus != "Terminated" && score >= PassingScore;
        attempt.Status = finalStatus switch
        {
            "Terminated" => "Terminated",
            "TimedOut" => "TimedOut",
            _ => attempt.Passed ? "Passed" : "Failed"
        };
        attempt.SubmittedAtUtc = submittedAtUtc;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<(List<int> EligibleIds, List<int> UnseenIds, List<int> UsedIds, string? Error)> GetQuestionAvailabilityDataAsync(
        string userId,
        int courseId,
        int topicId,
        CancellationToken cancellationToken)
    {
        var eligibleIds = await GetEligibleQuestionIdsAsync(topicId, cancellationToken);
        if (eligibleIds.Count == 0)
        {
            return (
                eligibleIds,
                new List<int>(),
                new List<int>(),
                "Exam questions are not available for this topic right now. Please try applying again later.");
        }

        if (eligibleIds.Count < QuestionCount)
        {
            return (
                eligibleIds,
                new List<int>(),
                new List<int>(),
                $"This topic currently has {eligibleIds.Count} valid exam question(s). A complete exam requires {QuestionCount} valid questions. Please try applying again later.");
        }

        var previouslyUsed = await GetPreviouslyUsedQuestionIdsAsync(
            userId,
            courseId,
            topicId,
            cancellationToken);
        var unseenIds = eligibleIds.Where(id => !previouslyUsed.Contains(id)).ToList();
        var usedIds = eligibleIds.Where(previouslyUsed.Contains).ToList();

        // Re-exams may reuse earlier questions only when at least 45 valid unseen questions remain.
        if (unseenIds.Count < QuestionCount - 5)
        {
            return (
                eligibleIds,
                unseenIds,
                usedIds,
                $"Only {unseenIds.Count} unseen valid questions remain. A re-exam needs at least {QuestionCount - 5} unseen questions. Please try applying again later.");
        }

        return (eligibleIds, unseenIds, usedIds, null);
    }

    private async Task<List<int>> GetEligibleQuestionIdsAsync(int topicId, CancellationToken cancellationToken)
    {
        return await _db.TopicExamQuestions.AsNoTracking()
            .Where(q => q.Flag == 0
                && q.TopicId == topicId
                && q.CorrectOptionNumber >= 1 && q.CorrectOptionNumber <= 4
                && !string.IsNullOrWhiteSpace(q.Question)
                && !string.IsNullOrWhiteSpace(q.Option1) && !string.IsNullOrWhiteSpace(q.Option2)
                && !string.IsNullOrWhiteSpace(q.Option3) && !string.IsNullOrWhiteSpace(q.Option4)
                && q.Option1 != q.Option2 && q.Option1 != q.Option3 && q.Option1 != q.Option4
                && q.Option2 != q.Option3 && q.Option2 != q.Option4 && q.Option3 != q.Option4)
            .Select(q => q.Id).Distinct().ToListAsync(cancellationToken);
    }

    private async Task<HashSet<int>> GetPreviouslyUsedQuestionIdsAsync(string userId, int courseId, int topicId, CancellationToken cancellationToken)
    {
        var snapshots = await _db.TopicExamAttempts.AsNoTracking()
            .Where(x => x.UserId == userId && x.CourseId == courseId && x.TopicId == topicId && x.StartedAtUtc.HasValue)
            .Select(x => x.QuestionIdsJson).ToListAsync(cancellationToken);
        var result = new HashSet<int>();
        foreach (var snapshot in snapshots)
            foreach (var id in DeserializeIds(snapshot)) result.Add(id);
        return result;
    }

    private async Task<TopicExamAttempt?> FindOwnedAttemptAsync(string userId, long attemptId, CancellationToken cancellationToken) =>
        await _db.TopicExamAttempts.SingleOrDefaultAsync(x => x.Id == attemptId && x.UserId == userId, cancellationToken);

    private async Task<bool> ClaimDeviceAsync(TopicExamAttempt attempt, string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token)) return false;
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

        var claimed = await _db.TopicExamAttempts
            .Where(x => x.Id == attempt.Id
                && x.UserId == attempt.UserId
                && (x.DeviceTokenHash == null || x.DeviceTokenHash == hash))
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(x => x.DeviceTokenHash, hash),
                cancellationToken);

        if (claimed == 0) return false;
        await _db.Entry(attempt).ReloadAsync(cancellationToken);
        return string.Equals(attempt.DeviceTokenHash, hash, StringComparison.Ordinal);
    }

    private static Task<bool> DeviceMatchesAsync(TopicExamAttempt attempt, string token)
    {
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(attempt.DeviceTokenHash))
            return Task.FromResult(false);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        try
        {
            return Task.FromResult(CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(attempt.DeviceTokenHash), Convert.FromHexString(hash)));
        }
        catch (FormatException) { return Task.FromResult(false); }
    }

    private async Task<TopicExamSessionDto> BuildSessionAsync(TopicExamAttempt attempt, bool includeAnswers, bool includeQuestions, CancellationToken cancellationToken)
    {
        var ids = DeserializeIds(attempt.QuestionIdsJson);
        var questions = includeQuestions ? await _db.TopicExamQuestions.AsNoTracking().Where(q => ids.Contains(q.Id)).ToListAsync(cancellationToken) : new List<TopicExamQuestion>();
        var answerMap = includeAnswers
            ? JsonSerializer.Deserialize<Dictionary<int, int?>>(attempt.AnswersJson, JsonOptions) ?? new()
            : new Dictionary<int, int?>();
        var mapped = includeQuestions ? ids.Where(id => questions.Any(q => q.Id == id)).Select(id =>
        {
            var q = questions.Single(x => x.Id == id);
            return new TopicExamQuestionDto
            {
                QuestionId = q.Id,
                Question = q.Question ?? string.Empty,
                Options = GetShuffledOptions(q, attempt.OptionOrderJson),
                SelectedOptionNumber = answerMap.GetValueOrDefault(id)
            };
        }).ToList() : new List<TopicExamQuestionDto>();
        var topicTitle = await _db.Topics.AsNoTracking()
            .Where(topic => topic.Id == attempt.TopicId)
            .Select(topic => topic.Title)
            .SingleOrDefaultAsync(cancellationToken) ?? "Topic Examination";
        return new TopicExamSessionDto
        {
            AttemptId = attempt.Id, TopicTitle = topicTitle, ScheduledAtUtc = attempt.ScheduledAtUtc,
            VerificationStartedAtUtc = attempt.VerificationStartedAtUtc,
            DeadlineAtUtc = attempt.DeadlineAtUtc, Status = attempt.Status, Questions = mapped
        };
    }

    private async Task<TopicExamSummaryDto> MapSummaryAsync(TopicExamAttempt attempt, CancellationToken cancellationToken)
    {
        var title = await _db.Topics.AsNoTracking().Where(t => t.Id == attempt.TopicId)
            .Select(t => t.Title).SingleOrDefaultAsync(cancellationToken) ?? "Topic";
        return new TopicExamSummaryDto
        {
            AttemptId = attempt.Id, CourseId = attempt.CourseId, TopicId = attempt.TopicId,
            TopicTitle = title, ScheduledAtUtc = attempt.ScheduledAtUtc,
            StartedAtUtc = attempt.StartedAtUtc, DeadlineAtUtc = attempt.DeadlineAtUtc,
            SubmittedAtUtc = attempt.SubmittedAtUtc, Status = attempt.Status, Score = attempt.Score,
            TotalQuestions = attempt.TotalQuestions, Passed = attempt.Passed,
            WarningCount = attempt.WarningCount, TerminationReason = attempt.TerminationReason
        };
    }

    private static IReadOnlyList<string> GetShuffledOptions(TopicExamQuestion question, string orderJson)
    {
        var original = new[]
        {
            question.Option1 ?? string.Empty,
            question.Option2 ?? string.Empty,
            question.Option3 ?? string.Empty,
            question.Option4 ?? string.Empty
        };
        var orders = DeserializeOptionOrders(orderJson);
        if (!orders.TryGetValue(question.Id, out var order) || order.Count != 4
            || order.Any(index => index is < 1 or > 4) || order.Distinct().Count() != 4)
        {
            return original;
        }
        return order.Select(index => original[index - 1]).ToArray();
    }

    private static Dictionary<int, List<int>> DeserializeOptionOrders(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<int, List<int>>>(json, JsonOptions) ?? new();
        }
        catch (JsonException)
        {
            return new();
        }
    }

    private static List<int> DeserializeIds(string json)
    {
        try { return JsonSerializer.Deserialize<List<int>>(json, JsonOptions) ?? new(); }
        catch (JsonException) { return new(); }
    }
}
