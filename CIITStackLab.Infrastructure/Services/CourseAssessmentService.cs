using System.Text.Json;
using CIITStackLab.Application.DTOs;
using CIITStackLab.Application.Interfaces;
using CIITStackLab.Domain.Entities;
using CIITStackLab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CIITStackLab.Infrastructure.Services;

public sealed class CourseAssessmentService : ICourseAssessmentService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApplicationDbContext _dbContext;

    public CourseAssessmentService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AssessmentSubmissionOutcomeDto> SubmitAsync(
        string userId,
        int courseId,
        int? contentId,
        int? topicId,
        IReadOnlyList<AssessmentAnswerDto> answers,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId)
            || courseId <= 0
            || (contentId is null && topicId is null)
            || contentId is <= 0
            || topicId is <= 0)
        {
            return AssessmentSubmissionOutcomeDto.Failure(
                "The assessment request is not valid. Reload the lesson and try again.");
        }

        List<ContentQuestion> questions;

        if (contentId.HasValue)
        {
            var lessonBelongsToCourse = await IsActiveCourseLessonAsync(
                courseId,
                contentId.Value,
                cancellationToken);

            if (!lessonBelongsToCourse)
            {
                return AssessmentSubmissionOutcomeDto.Failure(
                    "This lesson is no longer available in the selected course.");
            }

            var lessonTopicId = await _dbContext.Lessons
                .AsNoTracking()
                .Where(lesson => lesson.Id == contentId.Value && lesson.Flag == 0)
                .Select(lesson => lesson.TopicId)
                .SingleOrDefaultAsync(cancellationToken);

            var lessonNoteId = await _dbContext.Lessons
                .AsNoTracking()
                .Where(lesson => lesson.Id == contentId.Value && lesson.Flag == 0)
                .Select(lesson => lesson.NoteId)
                .SingleOrDefaultAsync(cancellationToken);

            int? firstActiveContentId = null;
            int? firstActiveChapterContentId = null;

            if (lessonTopicId.HasValue)
            {
                firstActiveContentId = await _dbContext.Lessons
                    .AsNoTracking()
                    .Where(lesson => lesson.TopicId == lessonTopicId.Value && lesson.Flag == 0)
                    .OrderBy(lesson => lesson.Id)
                    .Select(lesson => (int?)lesson.Id)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            if (lessonNoteId.HasValue)
            {
                firstActiveChapterContentId = await _dbContext.Lessons
                    .AsNoTracking()
                    .Where(lesson => lesson.NoteId == lessonNoteId.Value && lesson.Flag == 0)
                    .OrderBy(lesson => lesson.Id)
                    .Select(lesson => (int?)lesson.Id)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            questions = await _dbContext.ContentQuestions
                .AsNoTracking()
                .Where(question =>
                    question.Flag == 0
                    && (question.ContentId == contentId.Value
                        || (lessonTopicId.HasValue
                            && firstActiveContentId == contentId.Value
                            && question.TopicId == lessonTopicId.Value)
                        || (lessonNoteId.HasValue
                            && firstActiveChapterContentId == contentId.Value
                            && question.NoteId == lessonNoteId.Value)))
                .OrderBy(question => question.Id)
                .ToListAsync(cancellationToken);
        }
        else
        {
            var topicBelongsToCourse = await IsActiveCourseTopicAsync(
                courseId,
                topicId!.Value,
                cancellationToken);

            if (!topicBelongsToCourse)
            {
                return AssessmentSubmissionOutcomeDto.Failure(
                    "This topic is no longer available in the selected course.");
            }

            var topicNoteIds = await _dbContext.TrainingNotes
                .AsNoTracking()
                .Where(note => note.TopicId == topicId.Value && note.Flag == 0)
                .Select(note => note.Id)
                .ToListAsync(cancellationToken);

            var topicLessonIds = await _dbContext.Lessons
                .AsNoTracking()
                .Where(lesson => lesson.TopicId == topicId.Value && lesson.Flag == 0)
                .Select(lesson => lesson.Id)
                .ToListAsync(cancellationToken);

            questions = await _dbContext.ContentQuestions
                .AsNoTracking()
                .Where(question =>
                    question.Flag == 0
                    && (question.TopicId == topicId.Value
                        || (question.NoteId.HasValue && topicNoteIds.Contains(question.NoteId.Value))
                        || (question.ContentId.HasValue && topicLessonIds.Contains(question.ContentId.Value))))
                .OrderBy(question => question.Id)
                .ToListAsync(cancellationToken);
        }

        if (questions.Count == 0)
        {
            return AssessmentSubmissionOutcomeDto.Failure(
                "There are no active assessment questions for this lesson or chapter.");
        }

        var activeQuestionIds = questions
            .Select(question => question.Id)
            .ToHashSet();

        var existingAttemptResult = await GetExistingAttemptResultAsync(
            userId,
            courseId,
            contentId,
            activeQuestionIds,
            cancellationToken);

        if (existingAttemptResult is not null)
        {
            // A previous submission is final. Return its stored result instead of
            // treating a second click/retry as a failed request. This also lets the
            // inline UI render an already-completed assessment after a page reload.
            return AssessmentSubmissionOutcomeDto.Success(existingAttemptResult);
        }

        if (answers is null || answers.Count != questions.Count)
        {
            return AssessmentSubmissionOutcomeDto.Failure(
                "The submitted answers do not match this assessment. Reload the lesson and try again.");
        }

        var answersByQuestionId = new Dictionary<int, AssessmentAnswerDto>();

        foreach (var answer in answers)
        {
            if (answer is null
                || answer.QuestionId <= 0
                || !answersByQuestionId.TryAdd(answer.QuestionId, answer))
            {
                return AssessmentSubmissionOutcomeDto.Failure(
                    "The assessment contains duplicate or invalid question identifiers.");
            }
        }

        if (!answersByQuestionId.Keys.ToHashSet().SetEquals(activeQuestionIds))
        {
            return AssessmentSubmissionOutcomeDto.Failure(
                "The submitted questions do not match this lesson. Reload and try again.");
        }

        var reviewedQuestions = new List<AssessmentQuestionResultDto>(questions.Count);
        var score = 0;

        foreach (var question in questions)
        {
            var options = new[]
            {
                question.Option1,
                question.Option2,
                question.Option3,
                question.Option4
            };

            if (!question.CorrectOptionNumber.HasValue
                || question.CorrectOptionNumber.Value < 1
                || question.CorrectOptionNumber.Value > options.Length
                || string.IsNullOrWhiteSpace(options[question.CorrectOptionNumber.Value - 1]))
            {
                return AssessmentSubmissionOutcomeDto.Failure(
                    "This assessment is not configured correctly. Please contact an administrator.");
            }

            var answer = answersByQuestionId[question.Id];
            var selectedOption = answer.SelectedOptionNumber;

            if (selectedOption.HasValue
                && (selectedOption.Value < 1
                    || selectedOption.Value > options.Length
                    || string.IsNullOrWhiteSpace(options[selectedOption.Value - 1])))
            {
                return AssessmentSubmissionOutcomeDto.Failure(
                    "One or more selected options are invalid. Reload the lesson and try again.");
            }

            var correctOption = question.CorrectOptionNumber.Value;
            var isCorrect = selectedOption == correctOption;

            if (isCorrect)
            {
                score++;
            }

            reviewedQuestions.Add(new AssessmentQuestionResultDto
            {
                QuestionId = question.Id,
                Question = question.Question ?? string.Empty,
                Options = options,
                SelectedOptionNumber = selectedOption,
                CorrectOptionNumber = correctOption,
                IsCorrect = isCorrect
            });
        }

        var submittedAt = DateTime.UtcNow;
        var attempt = new StudentAssessmentAttempt
        {
            UserId = userId,
            CourseId = courseId,
            ContentId = contentId,
            Score = score,
            TotalQuestions = reviewedQuestions.Count,
            AnswersJson = JsonSerializer.Serialize(reviewedQuestions, JsonOptions),
            SubmittedAt = submittedAt
        };

        _dbContext.StudentAssessmentAttempts.Add(attempt);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return AssessmentSubmissionOutcomeDto.Success(MapResult(attempt, reviewedQuestions));
    }

    public async Task<AssessmentAttemptResultDto?> GetAttemptAsync(
        string userId,
        int courseId,
        int attemptId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId)
            || courseId <= 0
            || attemptId <= 0)
        {
            return null;
        }

        var attempt = await _dbContext.StudentAssessmentAttempts
            .AsNoTracking()
            .SingleOrDefaultAsync(
                row => row.Id == attemptId
                    && row.UserId == userId
                    && row.CourseId == courseId,
                cancellationToken);

        if (attempt is null)
        {
            return null;
        }

        List<AssessmentQuestionResultDto>? questions;

        try
        {
            questions = JsonSerializer.Deserialize<List<AssessmentQuestionResultDto>>(
                attempt.AnswersJson,
                JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }

        if (questions is null || questions.Count != attempt.TotalQuestions)
        {
            return null;
        }

        return MapResult(attempt, questions);
    }

    private async Task<AssessmentAttemptResultDto?> GetExistingAttemptResultAsync(
        string userId,
        int courseId,
        int? contentId,
        IReadOnlySet<int> activeQuestionIds,
        CancellationToken cancellationToken)
    {
        if (contentId.HasValue)
        {
            var attempt = await _dbContext.StudentAssessmentAttempts
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    row => row.UserId == userId
                        && row.CourseId == courseId
                        && row.ContentId == contentId.Value,
                    cancellationToken);

            return DeserializeAttemptResult(attempt);
        }

        var topicAttempts = await _dbContext.StudentAssessmentAttempts
            .AsNoTracking()
            .Where(attempt => attempt.UserId == userId
                && attempt.CourseId == courseId
                && attempt.ContentId == null)
            .OrderByDescending(attempt => attempt.SubmittedAt)
            .ToListAsync(cancellationToken);

        foreach (var attempt in topicAttempts)
        {
            var result = DeserializeAttemptResult(attempt);
            if (result is null || result.Questions.Count != activeQuestionIds.Count)
            {
                continue;
            }

            var previousQuestionIds = result.Questions
                .Select(question => question.QuestionId)
                .ToHashSet();

            if (previousQuestionIds.SetEquals(activeQuestionIds))
            {
                return result;
            }
        }

        return null;
    }

    private static AssessmentAttemptResultDto? DeserializeAttemptResult(
        StudentAssessmentAttempt? attempt)
    {
        if (attempt is null)
        {
            return null;
        }

        try
        {
            var questions = JsonSerializer.Deserialize<List<AssessmentQuestionResultDto>>(
                attempt.AnswersJson,
                JsonOptions);

            if (questions is null || questions.Count != attempt.TotalQuestions)
            {
                return null;
            }

            return MapResult(attempt, questions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task<bool> IsActiveCourseLessonAsync(
        int courseId,
        int contentId,
        CancellationToken cancellationToken)
    {
        return await (
            from course in _dbContext.Courses.AsNoTracking()
            join courseTopic in _dbContext.CourseModules.AsNoTracking()
                on course.Id equals courseTopic.CourseId
            join topic in _dbContext.Topics.AsNoTracking()
                on courseTopic.TopicId equals topic.Id
            join lesson in _dbContext.Lessons.AsNoTracking()
                on (int?)topic.Id equals lesson.TopicId
            where course.Id == courseId
                  && course.Flag == 0
                  && courseTopic.Flag == 0
                  && topic.Flag == 0
                  && lesson.Flag == 0
                  && lesson.Id == contentId
            select lesson.Id)
            .AnyAsync(cancellationToken);
    }

    private async Task<bool> IsActiveCourseTopicAsync(
        int courseId,
        int topicId,
        CancellationToken cancellationToken)
    {
        return await (
            from course in _dbContext.Courses.AsNoTracking()
            join courseTopic in _dbContext.CourseModules.AsNoTracking()
                on course.Id equals courseTopic.CourseId
            join topic in _dbContext.Topics.AsNoTracking()
                on courseTopic.TopicId equals topic.Id
            where course.Id == courseId
                  && course.Flag == 0
                  && courseTopic.Flag == 0
                  && topic.Flag == 0
                  && topic.Id == topicId
            select topic.Id)
            .AnyAsync(cancellationToken);
    }

    private static AssessmentAttemptResultDto MapResult(
        StudentAssessmentAttempt attempt,
        IReadOnlyList<AssessmentQuestionResultDto> questions)
    {
        var percentage = attempt.TotalQuestions == 0
            ? 0
            : (int)Math.Round(
                attempt.Score * 100d / attempt.TotalQuestions,
                MidpointRounding.AwayFromZero);

        return new AssessmentAttemptResultDto
        {
            Id = attempt.Id,
            CourseId = attempt.CourseId,
            ContentId = attempt.ContentId,
            Score = attempt.Score,
            TotalQuestions = attempt.TotalQuestions,
            Percentage = percentage,
            SubmittedAt = attempt.SubmittedAt,
            Questions = questions
        };
    }
}
