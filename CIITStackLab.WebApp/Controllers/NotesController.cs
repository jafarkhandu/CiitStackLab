using System.Security.Claims;
using CIITStackLab.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CIITStackLab.WebApp.Controllers;

public sealed class NotesController : Controller
{
    private readonly INotesService _notesService;
    private readonly ICourseEnrollmentService _courseEnrollmentService;

    public NotesController(
        INotesService notesService,
        ICourseEnrollmentService courseEnrollmentService)
    {
        _notesService = notesService;
        _courseEnrollmentService = courseEnrollmentService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        int topicId,
        string? chapterId,
        CancellationToken ct)
    {
        if (topicId <= 0)
        {
            return NotFound();
        }

        var userId = User.Identity?.IsAuthenticated == true
            ? User.FindFirstValue(ClaimTypes.NameIdentifier)
            : null;

        var canAccessTopic = await _courseEnrollmentService.CanAccessTopicAsync(
            userId,
            topicId,
            ct);

        if (!canAccessTopic)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                var returnUrl = Url.Action(nameof(Index), new { topicId, chapterId });
                return RedirectToAction(
                    "Login",
                    "Account",
                    new { returnUrl });
            }

            TempData["CourseEnrollmentMessage"] =
                "Enrollment is required to open notes for this paid course. Select the course and request enrollment first.";

            TempData["CourseEnrollmentMessageType"] = "error";

            return RedirectToAction("Index", "Home");
        }

        var notes = await _notesService.GetReaderAsync(
            topicId,
            chapterId,
            ct);

        return notes is null
            ? NotFound()
            : View(notes);
    }
}
