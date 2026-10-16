using System.Security.Claims;
using CIITStackLab.Application.DTOs;
using CIITStackLab.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CIITStackLab.WebApp.Controllers;

[Authorize(Roles = "Student")]
[Route("Examinations")]
public sealed class StudentExaminationsController : Controller
{
    private readonly ITopicExaminationService _exams;

    public StudentExaminationsController(ITopicExaminationService exams) => _exams = exams;

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();
        return View(await _exams.GetStudentExamsAsync(userId, cancellationToken));
    }

    [HttpPost("Schedule")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Schedule(TopicExamScheduleRequest request, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();
        var result = await _exams.ScheduleAsync(userId, request.CourseId, request.TopicId,
            DateTime.SpecifyKind(request.ScheduledAtLocal, DateTimeKind.Local).ToUniversalTime(), cancellationToken);
        TempData[result.Succeeded ? "ExamMessage" : "ExamError"] =
            result.Succeeded ? "Exam scheduled successfully." : result.Error;
        return RedirectToAction(nameof(Index));
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
    public async Task<IActionResult> Start(long attemptId, CancellationToken cancellationToken)
    {
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
    public async Task<IActionResult> Submit(long attemptId, [FromForm] List<TopicExamAnswerInput> answers, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();
        var deviceToken = Request.Cookies[DeviceCookieName(attemptId)] ?? string.Empty;
        // Device verification is also required for event logging; answer submission is scoped to the authenticated owner.
        var result = await _exams.SaveAnswersAsync(userId, attemptId, answers, cancellationToken);
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

    private static string DeviceCookieName(long attemptId) => $"ciit-exam-device-{attemptId}";
}
