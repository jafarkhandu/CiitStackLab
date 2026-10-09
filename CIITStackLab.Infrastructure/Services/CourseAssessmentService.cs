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
        int contentId,
        IReadOnlyList<AssessmentAnswerDto> answers,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || courseId <= 0 || contentId <= 0)
        {
            return AssessmentSubmissionOutcomeDto.Failure(
                "The assessment request is not valid. Reload the lesson and try again.");
        }

        var lessonBelongsToCourse = await IsActiveCourseLessonAsync(
            courseId,
            contentId,
            cancellationToken);

        if (!lessonBelongsToCourse)
        {
            return AssessmentSubmissionOutcomeDto.Failure(
                "This lesson is no longer available in the selected course.");
        }

        var questions = await _dbContext.ContentQuestions
            .AsNoTracking()
            .Where(question =>
                question.ContentId == contentId
                && question.Flag == 0)
            .OrderBy(question => question.Id)
            .ToListAsync(cancellationToken);

        if (questions.Count == 0)
        {
            return AssessmentSubmissionOutcomeDto.Failure(
                "There are no active assessment questions for this lesson.");
        }

        if (answers is null || answers.Count != questions.Count)
        {
            return AssessmentSubmissionOutcomeDto.Failure(
                "The submitted answers do not match this assessment. Reload the lesson and try again.");
        }

        var answersByQuestionId = new Dictionary<int, AssessmentAnswerDto>();

        foreach (var answer in answers)
        {
            if (answer.QuestionId <= 0
                || !answersByQuestionId.TryAdd(answer.QuestionId, answer))
            {
                return AssessmentSubmissionOutcomeDto.Failure(
                    "The assessment contains duplicate or invalid question identifiers.");
            }
        }

        var activeQuestionIds = questions
            .Select(question => question.Id)
            .ToHashSet();

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
