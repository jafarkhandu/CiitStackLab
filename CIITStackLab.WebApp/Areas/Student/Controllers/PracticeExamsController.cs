using System.Security.Claims;
using System.Text.Json;
using CIITStackLab.Domain.Entities;
using CIITStackLab.Infrastructure.Persistence;
using CIITStackLab.WebApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CIITStackLab.WebApp.Controllers;

[Authorize(Roles = "Student")]
public sealed class PracticeExamsController : Controller
{
    private const int ExamDurationMinutes = 30;
    private const int MaximumQuestions = 20;
    private const int MaximumRepeatedQuestionsPerAttempt = 5;
    private const int MaximumRepeatUsesPerQuestion = 5;

    private readonly ApplicationDbContext _dbContext;

    public PracticeExamsController(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("/PracticeExams/Start/{courseId:int}/{topicId:int}")]
    public async Task<IActionResult> Start(
        int courseId,
        int topicId,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        var course = await _dbContext.Courses
            .AsNoTracking()
            .Where(item => item.Id == courseId && item.Flag == 0 && item.DeletedAt == null)
            .Select(item => new { item.Id, item.Title })
            .SingleOrDefaultAsync(cancellationToken);

        if (course is null)
        {
            return NotFound();
        }

        var topic = await (
            from module in _dbContext.CourseModules.AsNoTracking()
            join item in _dbContext.Topics.AsNoTracking()
                on module.TopicId equals item.Id
            where module.CourseId == courseId
                  && module.TopicId == topicId
                  && module.Flag == 0
                  && module.DeletedAt == null
                  && item.Flag == 0
                  && item.DeletedAt == null
            select new { item.Id, item.Title })
            .SingleOrDefaultAsync(cancellationToken);

        if (topic is null)
        {
            return NotFound();
        }

        var now = DateTime.UtcNow;
        var activeAttempt = await _dbContext.PracticeExamAttempts
            .Where(attempt => attempt.UserId == userId
                && attempt.CourseId == courseId
                && attempt.TopicId == topicId
                && attempt.SubmittedAtUtc == null)
            .OrderByDescending(attempt => attempt.StartedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (activeAttempt is not null)
        {
            if (now >= activeAttempt.StartedAtUtc.AddMinutes(ExamDurationMinutes))
            {
                var expiredQuestionIds = DeserializeQuestionIds(activeAttempt.QuestionIdsJson);
                var savedAnswers = NormalizeAnswers(activeAttempt.AnswersJson, expiredQuestionIds);
                activeAttempt.AnswersJson = JsonSerializer.Serialize(savedAnswers);
                activeAttempt.TotalQuestions = expiredQuestionIds.Count;
                activeAttempt.Score = await CalculateScoreAsync(
                    expiredQuestionIds,
                    savedAnswers,
                    cancellationToken);
                activeAttempt.SubmittedAtUtc = activeAttempt.StartedAtUtc.AddMinutes(ExamDurationMinutes);
                await _dbContext.SaveChangesAsync(cancellationToken);
                return RedirectToAction(nameof(Result), new { attemptId = activeAttempt.Id });
            }

            return await RenderAttemptAsync(
                activeAttempt,
                course.Title,
                topic.Title,
                cancellationToken);
        }

        var lessonIds = await _dbContext.Lessons
            .AsNoTracking()
            .Where(lesson => lesson.TopicId == topicId
                && (lesson.Flag == 0 || lesson.Flag == null)
                && lesson.DeletedAt == null)
            .Select(lesson => lesson.Id)
            .ToListAsync(cancellationToken);

        var questionBank = await _dbContext.ContentQuestions
            .AsNoTracking()
            .Where(question =>
                (question.Flag == 0 || question.Flag == null)
                && question.DeletedAt == null
                && question.CorrectOptionNumber >= 1
                && question.CorrectOptionNumber <= 4
                && question.Question != null
                && question.Question != string.Empty
                && question.Option1 != null
                && question.Option2 != null
                && question.Option3 != null
                && question.Option4 != null
                && ((question.TopicId.HasValue && question.TopicId.Value == topicId)
                    || (question.ContentId.HasValue && lessonIds.Contains(question.ContentId.Value))
                    || (question.NoteId.HasValue && lessonIds.Contains(question.NoteId.Value))))
            .Select(question => new
            {
                question.Id,
                Question = question.Question!,
                Option1 = question.Option1!,
                Option2 = question.Option2!,
                Option3 = question.Option3!,
                Option4 = question.Option4!,
                CorrectOptionNumber = question.CorrectOptionNumber!.Value
            })
            .Distinct()
            .ToListAsync(cancellationToken);

        if (questionBank.Count == 0)
        {
            TempData["PracticeExamMessage"] = "No practice questions are available for this topic yet.";
            return RedirectToAction("PracticeAssessment", "Courses");
        }

        var previousAttempts = await _dbContext.PracticeExamAttempts
            .AsNoTracking()
            .Where(attempt => attempt.UserId == userId
                && attempt.CourseId == courseId
                && attempt.TopicId == topicId
                && attempt.SubmittedAtUtc != null)
            .Select(attempt => attempt.QuestionIdsJson)
            .ToListAsync(cancellationToken);

        var appearanceCounts = new Dictionary<int, int>();
        foreach (var questionIdsJson in previousAttempts)
        {
            var previousQuestionIds = DeserializeQuestionIds(questionIdsJson);
            foreach (var questionId in previousQuestionIds)
            {
                appearanceCounts[questionId] = appearanceCounts.GetValueOrDefault(questionId) + 1;
            }
        }

        var unseenQuestions = questionBank
            .Where(question => !appearanceCounts.ContainsKey(question.Id))
            .OrderBy(_ => Random.Shared.Next())
            .ToList();

        var selectedQuestions = unseenQuestions
            .Take(MaximumQuestions)
            .ToList();

        var remainingSlots = MaximumQuestions - selectedQuestions.Count;
        if (remainingSlots > 0)
        {
            var repeatQuestions = questionBank
                .Where(question => appearanceCounts.TryGetValue(question.Id, out var appearances)
                    && appearances <= MaximumRepeatUsesPerQuestion)
                .OrderBy(_ => Random.Shared.Next())
                .Take(Math.Min(MaximumRepeatedQuestionsPerAttempt, remainingSlots))
                .ToList();

            selectedQuestions.AddRange(repeatQuestions);
        }

        if (selectedQuestions.Count == 0)
        {
            TempData["PracticeExamMessage"] = "The Limit For Practice Examination Is Over";
            return RedirectToAction("PracticeAssessment", "Courses");
        }

        var attempt = new PracticeExamAttempt
        {
            UserId = userId,
            CourseId = courseId,
            TopicId = topicId,
            QuestionIdsJson = JsonSerializer.Serialize(selectedQuestions.Select(question => question.Id).ToList()),
            AnswersJson = "{}",
            Score = 0,
            TotalQuestions = selectedQuestions.Count,
            StartedAtUtc = now
        };

        _dbContext.PracticeExamAttempts.Add(attempt);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return View("TakeExam", new PracticeExamTakeViewModel
        {
            AttemptId = attempt.Id,
            CourseId = courseId,
            TopicId = topicId,
            CourseTitle = course.Title,
            TopicTitle = topic.Title,
            StartedAtUtc = attempt.StartedAtUtc,
            Questions = selectedQuestions.Select(question => new PracticeExamQuestionViewModel
            {
                QuestionId = question.Id,
                Question = question.Question,
                Option1 = question.Option1,
                Option2 = question.Option2,
                Option3 = question.Option3,
                Option4 = question.Option4
            }).ToList()
        });
    }

    [HttpPost("/PracticeExams/SaveAnswers/{attemptId:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveAnswers(
        int attemptId,
        string? answersJson,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var attempt = await _dbContext.PracticeExamAttempts
            .SingleOrDefaultAsync(item => item.Id == attemptId && item.UserId == userId, cancellationToken);

        if (attempt is null)
        {
            return NotFound();
        }

        if (attempt.SubmittedAtUtc.HasValue
            || DateTime.UtcNow >= attempt.StartedAtUtc.AddMinutes(ExamDurationMinutes))
        {
            return StatusCode(StatusCodes.Status409Conflict);
        }

        var questionIds = DeserializeQuestionIds(attempt.QuestionIdsJson);
        var answers = NormalizeAnswers(answersJson, questionIds);
        attempt.AnswersJson = JsonSerializer.Serialize(answers);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpPost("/PracticeExams/Submit/{attemptId:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(
        int attemptId,
        string? answersJson,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        var attempt = await _dbContext.PracticeExamAttempts
            .SingleOrDefaultAsync(item => item.Id == attemptId && item.UserId == userId, cancellationToken);

        if (attempt is null)
        {
            return NotFound();
        }

        if (attempt.SubmittedAtUtc.HasValue)
        {
            return RedirectToAction(nameof(Result), new { attemptId });
        }

        var selectedQuestionIds = DeserializeQuestionIds(attempt.QuestionIdsJson);
        var isWithinDeadline = DateTime.UtcNow < attempt.StartedAtUtc.AddMinutes(ExamDurationMinutes);
        var submittedAnswers = NormalizeAnswers(
            isWithinDeadline ? answersJson : attempt.AnswersJson,
            selectedQuestionIds);
        var score = await CalculateScoreAsync(
            selectedQuestionIds,
            submittedAnswers,
            cancellationToken);

        attempt.AnswersJson = JsonSerializer.Serialize(submittedAnswers);
        attempt.Score = score;
        attempt.TotalQuestions = selectedQuestionIds.Count;
        attempt.SubmittedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Result), new { attemptId });
    }

    [HttpGet("/PracticeExams/Result/{attemptId:int}")]
    public async Task<IActionResult> Result(int attemptId, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        var attempt = await _dbContext.PracticeExamAttempts
            .AsNoTracking()
            .Where(item => item.Id == attemptId && item.UserId == userId && item.SubmittedAtUtc != null)
            .Select(item => new
            {
                item.Id,
                item.CourseId,
                item.TopicId,
                item.Score,
                item.TotalQuestions,
                item.StartedAtUtc,
                SubmittedAtUtc = item.SubmittedAtUtc!.Value
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (attempt is null)
        {
            return NotFound();
        }

        var courseTitle = await _dbContext.Courses
            .AsNoTracking()
            .Where(course => course.Id == attempt.CourseId)
            .Select(course => course.Title)
            .SingleOrDefaultAsync(cancellationToken) ?? "Course";
        var topicTitle = await _dbContext.Topics
            .AsNoTracking()
            .Where(topic => topic.Id == attempt.TopicId)
            .Select(topic => topic.Title)
            .SingleOrDefaultAsync(cancellationToken) ?? "Topic";

        return View(new PracticeExamResultViewModel
        {
            AttemptId = attempt.Id,
            CourseTitle = courseTitle,
            TopicTitle = topicTitle,
            Score = attempt.Score,
            TotalQuestions = attempt.TotalQuestions,
            StartedAtUtc = attempt.StartedAtUtc,
            SubmittedAtUtc = attempt.SubmittedAtUtc
        });
    }

    private async Task<IActionResult> RenderAttemptAsync(
        PracticeExamAttempt attempt,
        string courseTitle,
        string topicTitle,
        CancellationToken cancellationToken)
    {
        var questionIds = DeserializeQuestionIds(attempt.QuestionIdsJson);
        var questions = await _dbContext.ContentQuestions
            .AsNoTracking()
            .Where(question => questionIds.Contains(question.Id))
            .Select(question => new
            {
                question.Id,
                Question = question.Question ?? string.Empty,
                Option1 = question.Option1 ?? string.Empty,
                Option2 = question.Option2 ?? string.Empty,
                Option3 = question.Option3 ?? string.Empty,
                Option4 = question.Option4 ?? string.Empty
            })
            .ToListAsync(cancellationToken);

        var order = questionIds
            .Select((id, index) => new { id, index })
            .ToDictionary(item => item.id, item => item.index);

        return View("TakeExam", new PracticeExamTakeViewModel
        {
            AttemptId = attempt.Id,
            CourseId = attempt.CourseId,
            TopicId = attempt.TopicId,
            CourseTitle = courseTitle,
            TopicTitle = topicTitle,
            StartedAtUtc = attempt.StartedAtUtc,
            Questions = questions
                .OrderBy(question => order.GetValueOrDefault(question.Id, int.MaxValue))
                .Select(question => new PracticeExamQuestionViewModel
                {
                    QuestionId = question.Id,
                    Question = question.Question,
                    Option1 = question.Option1,
                    Option2 = question.Option2,
                    Option3 = question.Option3,
                    Option4 = question.Option4
                })
                .ToList()
        });
    }

    private static Dictionary<int, int> NormalizeAnswers(
        string? answersJson,
        IReadOnlyCollection<int> selectedQuestionIds)
    {
        Dictionary<int, int> answers;
        try
        {
            answers = string.IsNullOrWhiteSpace(answersJson)
                ? new Dictionary<int, int>()
                : JsonSerializer.Deserialize<Dictionary<int, int>>(answersJson)
                    ?? new Dictionary<int, int>();
        }
        catch (JsonException)
        {
            answers = new Dictionary<int, int>();
        }

        var allowedQuestionIds = selectedQuestionIds.ToHashSet();
        return answers
            .Where(answer => allowedQuestionIds.Contains(answer.Key)
                && answer.Value is >= 1 and <= 4)
            .ToDictionary(answer => answer.Key, answer => answer.Value);
    }

    private async Task<int> CalculateScoreAsync(
        IReadOnlyCollection<int> selectedQuestionIds,
        IReadOnlyDictionary<int, int> answers,
        CancellationToken cancellationToken)
    {
        var correctAnswers = await _dbContext.ContentQuestions
            .AsNoTracking()
            .Where(question => selectedQuestionIds.Contains(question.Id))
            .Select(question => new { question.Id, question.CorrectOptionNumber })
            .ToDictionaryAsync(
                question => question.Id,
                question => question.CorrectOptionNumber,
                cancellationToken);

        return selectedQuestionIds.Count(questionId =>
            answers.TryGetValue(questionId, out var selectedOption)
            && correctAnswers.TryGetValue(questionId, out var correctOption)
            && correctOption == selectedOption);
    }

    private static List<int> DeserializeQuestionIds(string? questionIdsJson)
    {
        try
        {
            return string.IsNullOrWhiteSpace(questionIdsJson)
                ? new List<int>()
                : JsonSerializer.Deserialize<List<int>>(questionIdsJson)?.Distinct().ToList()
                    ?? new List<int>();
        }
        catch (JsonException)
        {
            return new List<int>();
        }
    }
}
