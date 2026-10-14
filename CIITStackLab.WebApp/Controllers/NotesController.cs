using CIITStackLab.Application.Interfaces;
using CIITStackLab.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace CIITStackLab.WebApp.Controllers;

public sealed class NotesController : Controller
{
    private readonly INotesService _notesService;
    private readonly CoursePurchaseService _coursePurchaseService;
    private readonly ICourseService _courseService;

    public NotesController(
        INotesService notesService,
        CoursePurchaseService coursePurchaseService,
        ICourseService courseService)
    {
        _notesService = notesService;
        _coursePurchaseService = coursePurchaseService;
        _courseService = courseService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        int topicId,
        string? chapterId,
        int? courseId,
        CancellationToken ct)
    {
        if (topicId <= 0)
        {
            return NotFound();
        }

        var userId = User.Identity?.IsAuthenticated == true
            ? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            : null;

        var canAccessTopic = await _coursePurchaseService.CanAccessTopicAsync(userId, topicId, ct);

        if (!canAccessTopic)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                var returnUrl = Url.Action(nameof(Index), new { topicId, chapterId, courseId });
                return RedirectToAction("Login", "Account", new { returnUrl });
            }

            TempData["CoursePaymentMessage"] =
                "Purchase is required to open notes for this paid course.";

            return RedirectToAction("Index", "Home");
        }

        var notes = await _notesService.GetReaderAsync(
            topicId,
            chapterId,
            ct);

        if (notes is null)
        {
            return NotFound();
        }

        if (courseId is > 0)
        {
            var course = await _courseService.GetLearningAsync(courseId.Value, null, ct);

            if (course is not null && course.Topics.Any(topic => topic.Id == topicId))
            {
                ViewData["LearningPathModel"] = course;
                ViewData["CourseId"] = course.Id.ToString();
                ViewData["CourseTitle"] = course.Title;
                ViewData["NotesTopicId"] = notes.TopicId.ToString();
                ViewData["NotesChapterId"] = notes.CurrentChapterId;
            }
        }

        return View(notes);
    }
}
