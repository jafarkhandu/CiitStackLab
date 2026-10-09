using System.Security.Claims;
using CIITStackLab.Application.DTOs;
using CIITStackLab.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CIITStackLab.WebApp.Controllers;

public class CoursesController : Controller
{
    private readonly ICourseService _courseService;
    private readonly IStudentProgressService _studentProgressService;
    private readonly ICourseAssessmentService _courseAssessmentService;

    public CoursesController(
        ICourseService courseService,
        IStudentProgressService studentProgressService,
        ICourseAssessmentService courseAssessmentService)
    {
        _courseService = courseService;
        _studentProgressService = studentProgressService;
        _courseAssessmentService = courseAssessmentService;
    }

    [HttpGet]
    public async Task<IActionResult> Details(
        int id,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            return NotFound();
        }

        var course = await _courseService.GetDetailsAsync(
            id,
            cancellationToken);

        return course is null
            ? NotFound()
            : View(course);
    }

    [HttpGet("Courses/Learn/{id:int}")]
    public async Task<IActionResult> Learn(
        int id,
        int? contentId,
        CancellationToken cancellationToken)
    {
        if (id <= 0 || (contentId.HasValue && contentId.Value <= 0))
        {
            return NotFound();
        }

        var publishedCourse = await _courseService.GetByIdAsync(
            id,
            cancellationToken);

        if (publishedCourse is null)
        {
            return NotFound();
        }

        var studentUserId = User.IsInRole("Student")
            ? User.FindFirstValue(ClaimTypes.NameIdentifier)
            : null;

        if (!string.IsNullOrWhiteSpace(studentUserId) && !contentId.HasValue)
        {
            var resumeContentId = await _studentProgressService.GetResumeContentIdAsync(
                studentUserId,
                id,
                cancellationToken);

            if (resumeContentId.HasValue)
            {
                contentId = resumeContentId.Value;
            }
        }

        var course = await _courseService.GetLearningAsync(
            id,
            contentId,
            cancellationToken);

        if (course is null)
        {
            return NotFound();
        }

        if (!string.IsNullOrWhiteSpace(studentUserId)
            && course.CurrentContentId.HasValue)
        {
            await _studentProgressService.RecordAccessAsync(
                studentUserId,
                id,
                course.CurrentContentId.Value,
                cancellationToken);

            course.Progress = await _studentProgressService.GetProgressAsync(
                studentUserId,
                id,
                cancellationToken);
        }

        return View(course);
    }

    [HttpPost("Courses/Learn/{id:int}/Complete/{contentId:int}")]
    [Authorize(Roles = "Student")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CompleteLesson(
        int id,
        int contentId,
        CancellationToken cancellationToken)
    {
        if (id <= 0 || contentId <= 0)
        {
            return NotFound();
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var markedCompleted = await _studentProgressService.MarkCompletedAsync(
            userId,
            id,
            contentId,
            cancellationToken);

        if (!markedCompleted)
        {
            return NotFound();
        }

        TempData["LearningProgressMessage"] = "Lesson marked as complete.";

        return RedirectToAction(
            nameof(Learn),
            new
            {
                id,
                contentId
            });
    }

    [HttpPost("Courses/Learn/{id:int}/Assessment/{contentId:int}")]
    [Authorize(Roles = "Student")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitAssessment(
        int id,
        int contentId,
        [FromForm] List<AssessmentAnswerDto> answers,
        CancellationToken cancellationToken)
    {
        if (id <= 0 || contentId <= 0)
        {
            return NotFound();
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var outcome = await _courseAssessmentService.SubmitAsync(
            userId,
            id,
            contentId,
            answers ?? new List<AssessmentAnswerDto>(),
            cancellationToken);

        if (!outcome.Succeeded || outcome.Result is null)
        {
            TempData["AssessmentError"] = outcome.ErrorMessage
                ?? "The assessment could not be submitted. Please try again.";

            return RedirectToAction(
                nameof(Learn),
                new
                {
                    id,
                    contentId
                });
        }

        return RedirectToAction(
            nameof(AssessmentResult),
            new
            {
                id,
                attemptId = outcome.Result.Id
            });
    }

    [HttpGet("Courses/Learn/{id:int}/AssessmentResult/{attemptId:int}")]
    [Authorize(Roles = "Student")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> AssessmentResult(
        int id,
        int attemptId,
        CancellationToken cancellationToken)
    {
        if (id <= 0 || attemptId <= 0)
        {
            return NotFound();
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var attempt = await _courseAssessmentService.GetAttemptAsync(
            userId,
            id,
            attemptId,
            cancellationToken);

        return attempt is null
            ? NotFound()
            : View(attempt);
    }
}
