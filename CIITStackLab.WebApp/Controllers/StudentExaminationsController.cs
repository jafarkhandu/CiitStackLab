using System.Security.Claims;
using CIITStackLab.Application.DTOs;
using CIITStackLab.Application.Interfaces;
using CIITStackLab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CIITStackLab.WebApp.Controllers;

[Authorize(Roles = "Student")]
[Route("Examinations")]
public sealed class StudentExaminationsController : Controller
{
    private readonly ITopicExaminationService _exams;
    private readonly ApplicationDbContext _db;

    public StudentExaminationsController(ITopicExaminationService exams, ApplicationDbContext db)
    {
        _exams = exams;
        _db = db;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();
        ViewBag.AvailableTopics = await (
            from cm in _db.CourseModules.AsNoTracking()
            join course in _db.Courses.AsNoTracking() on cm.CourseId equals course.Id
            join topic in _db.Topics.AsNoTracking() on cm.TopicId equals topic.Id
            where cm.Flag == 0 && course.Flag == 0 && topic.Flag == 0
                && (!course.FeesAmount.HasValue || course.FeesAmount <= 0 || _db.StudentCoursePurchases.Any(p => p.UserId == userId && p.CourseId == course.Id))
            orderby course.Title, topic.Title
            select new ExamTopicOption { CourseId = course.Id, CourseTitle = course.Title, TopicId = topic.Id, TopicTitle = topic.Title }
        ).ToListAsync(cancellationToken);
        return View(await _exams.GetStudentExamsAsync(userId, cancellationToken));
    }

    [HttpPost("Schedule")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Schedule(TopicExamScheduleRequest request, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();
        var result = await _exams.ScheduleAsync(userId, request.CourseId, request.TopicId,
            DateTime.SpecifyKind(request.ScheduledAtLocal.AddMinutes(request.TimezoneOffsetMinutes), DateTimeKind.Utc), cancellationToken);
        TempData[result.Succeeded ? "ExamMessage" : "ExamError"] =
            result.Succeeded ? "Exam scheduled successfully." : result.Error;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("StartNow")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartNow(TopicExamScheduleRequest request, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();
        var result = await _exams.ScheduleAsync(userId, request.CourseId, request.TopicId,
            DateTime.UtcNow, cancellationToken, startNow: true);
        if (!result.Succeeded || result.Exam is null)
        {
            TempData["ExamError"] = result.Error;
            return RedirectToAction("Learn", "Courses", new { id = request.CourseId, topicId = request.TopicId });
        }
        return RedirectToAction(nameof(Instructions), new { attemptId = result.Exam.AttemptId });
    }

    [HttpPost("ReExam")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReExam(int courseId, int topicId, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();

        // A retake is a new attempt; never reopen or accept answers for a terminated attempt.
        var result = await _exams.ScheduleAsync(
            userId, courseId, topicId, DateTime.UtcNow, cancellationToken, startNow: true);

        if (!result.Succeeded || result.Exam is null)
        {
            TempData["ExamError"] = result.Error ?? "A new exam attempt could not be created.";
            return RedirectToAction(nameof(Index));
        }

        return RedirectToAction(nameof(Instructions), new { attemptId = result.Exam.AttemptId });
    }

    [HttpGet("{attemptId:long}/Instructions")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> Instructions(long attemptId, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();
        var deviceToken = Request.Cookies[DeviceCookieName(attemptId)];
        if (string.IsNullOrWhiteSpace(deviceToken))
        {
            deviceToken = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
            Response.Cookies.Append(DeviceCookieName(attemptId), deviceToken, new CookieOptions
            {
                HttpOnly = true, Secure = Request.IsHttps, SameSite = SameSiteMode.Strict,
                Path = "/",
                IsEssential = true, Expires = DateTimeOffset.UtcNow.AddHours(3)
            });
        }
        var result = await _exams.OpenPreExamAsync(userId, attemptId, deviceToken, cancellationToken);
        if (!result.Succeeded || result.Session is null)
        {
            TempData["ExamError"] = result.Error;
            return RedirectToAction(nameof(Index));
        }
        return View("Instructions", result.Session);
    }

    [HttpPost("{attemptId:long}/Start")]
    [ValidateAntiForgeryToken]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> Start(long attemptId, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store, no-cache, max-age=0";
        Response.Headers.Pragma = "no-cache";
        Response.Headers.Expires = "0";

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();
        var deviceToken = Request.Cookies[DeviceCookieName(attemptId)] ?? string.Empty;
        var result = await _exams.StartAsync(userId, attemptId, deviceToken, cancellationToken);
        if (!result.Succeeded || result.Session is null)
        {
            TempData["ExamError"] = result.Error;
            return RedirectToAction(nameof(Index));
        }
        return View("TakeExam", result.Session);
    }

    [HttpPost("{attemptId:long}/Submit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(
        long attemptId,
        [FromForm] List<TopicExamAnswerInput> answers,
        [FromForm] bool finalSubmission,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();
        var deviceToken = Request.Cookies[DeviceCookieName(attemptId)] ?? string.Empty;
        // Device verification is also required for event logging; answer submission is scoped to the authenticated owner.
        var result = await _exams.SaveAnswersAsync(
            userId, attemptId, answers, deviceToken, cancellationToken, finalSubmission);
        if (!result.Succeeded) return BadRequest(new { error = result.Error });
        return Json(new { success = true, status = result.Result?.Status, score = result.Result?.Score, passed = result.Result?.Passed });
    }

    [HttpPost("{attemptId:long}/SecurityEvent")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SecurityEvent(long attemptId, TopicExamSecurityEventInput input, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();
        var deviceToken = Request.Cookies[DeviceCookieName(attemptId)] ?? string.Empty;
        var accepted = await _exams.RecordSecurityEventAsync(userId, attemptId, input.EventType, input.Details, deviceToken, cancellationToken);
        return accepted ? Ok() : BadRequest();
    }

    public sealed class ExamTopicOption
    {
        public int CourseId { get; init; }
        public string CourseTitle { get; init; } = string.Empty;
        public int TopicId { get; init; }
        public string TopicTitle { get; init; } = string.Empty;
    }

    private static string DeviceCookieName(long attemptId) => $"ciit-exam-device-{attemptId}";
}
