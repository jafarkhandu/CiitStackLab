using System.Security.Claims;
using CIITStackLab.Application.DTOs;
using CIITStackLab.Application.Interfaces;
using CIITStackLab.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CIITStackLab.WebApp.Controllers;

[Authorize(Roles = "Student")]
public sealed class AssessmentSubmissionController : Controller
{
    private readonly ICourseAssessmentService _courseAssessmentService;
    private readonly CoursePurchaseService _coursePurchaseService;

    public AssessmentSubmissionController(
        ICourseAssessmentService courseAssessmentService,
        CoursePurchaseService coursePurchaseService)
    {
        _courseAssessmentService = courseAssessmentService;
        _coursePurchaseService = coursePurchaseService;
    }

    [HttpPost("Courses/Learn/{id:int}/AssessmentInline/{contentId:int}")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> SubmitContentAssessment(
        int id,
        int contentId,
        [FromForm] List<AssessmentAnswerDto> answers,
        CancellationToken cancellationToken)
    {
        return SubmitAsync(id, contentId, null, answers, cancellationToken);
    }

    [HttpPost("Courses/Learn/{id:int}/TopicAssessmentInline/{topicId:int}")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> SubmitTopicAssessment(
        int id,
        int topicId,
        [FromForm] List<AssessmentAnswerDto> answers,
        CancellationToken cancellationToken)
    {
        return SubmitAsync(id, null, topicId, answers, cancellationToken);
    }

    private async Task<IActionResult> SubmitAsync(
        int id,
        int? contentId,
        int? topicId,
        List<AssessmentAnswerDto> answers,
        CancellationToken cancellationToken)
    {
        if (id <= 0
            || (contentId is null && topicId is null)
            || contentId is <= 0
            || topicId is <= 0)
        {
            return NotFound();
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var hasDemoAccess = TempData.Peek($"DemoPayment:{id}") is not null;
        var hasCourseAccess = hasDemoAccess
            || await _coursePurchaseService.CanAccessCourseAsync(userId, id, cancellationToken);

        if (!hasCourseAccess)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Payment is required before a paid-course assessment can be submitted."
            });
        }

        var outcome = await _courseAssessmentService.SubmitAsync(
            userId,
            id,
            contentId,
            topicId,
            answers ?? new List<AssessmentAnswerDto>(),
            cancellationToken);

        if (!outcome.Succeeded || outcome.Result is null)
        {
            return BadRequest(new
            {
                message = outcome.ErrorMessage
                    ?? "The assessment could not be submitted. Please reload the lesson and try again."
            });
        }

        return Json(new { result = outcome.Result });
    }
}
