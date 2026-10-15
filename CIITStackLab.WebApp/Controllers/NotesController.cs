using CIITStackLab.Application.Interfaces;
using CIITStackLab.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace CIITStackLab.WebApp.Controllers;

public sealed class NotesController : Controller
{
    private readonly INotesService _notesService;
    private readonly CoursePurchaseService _coursePurchaseService;
    private readonly ICourseService _courseService;
    private readonly IWebHostEnvironment _environment;

    public NotesController(
        INotesService notesService,
        CoursePurchaseService coursePurchaseService,
        ICourseService courseService,
        IWebHostEnvironment environment)
    {
        _notesService = notesService;
        _coursePurchaseService = coursePurchaseService;
        _courseService = courseService;
        _environment = environment;
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

    [HttpGet]
    public async Task<IActionResult> SlideReader(
        int topicId,
        string? chapterId,
        int contentId,
        int? courseId,
        CancellationToken ct)
    {
        if (topicId <= 0 || contentId <= 0)
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
                var returnUrl = Url.Action(
                    nameof(SlideReader),
                    new { topicId, chapterId, contentId, courseId });
                return RedirectToAction("Login", "Account", new { returnUrl });
            }

            TempData["CoursePaymentMessage"] =
                "Purchase is required to open slides for this paid course.";
            return RedirectToAction("Index", "Home");
        }

        var notes = await _notesService.GetReaderAsync(topicId, chapterId, ct);
        var media = notes?.CurrentChapter?.Media.FirstOrDefault(x => x.ContentId == contentId);

        if (media is null || string.IsNullOrWhiteSpace(media.Slides))
        {
            return NotFound();
        }

        if (courseId is > 0)
        {
            var course = await _courseService.GetLearningAsync(courseId.Value, null, ct);

            if (course is null || !course.Topics.Any(topic => topic.Id == topicId))
            {
                return NotFound();
            }

            ViewData["LearningPathModel"] = course;
            ViewData["CourseId"] = course.Id.ToString();
            ViewData["CourseTitle"] = course.Title;
        }

        var slideExtension = Path.GetExtension(media.Slides).ToLowerInvariant();
        var slideType = slideExtension switch
        {
            ".pdf" => "pdf",
            ".ppt" => "ppt",
            ".pptx" => "pptx",
            _ => null
        };

        if (slideType is null)
        {
            return NotFound();
        }

        ViewData["NotesTopicId"] = notes.TopicId.ToString();
        ViewData["NotesChapterId"] = notes.CurrentChapterId;
        ViewData["SlideUrl"] = Url.Action(
            nameof(SlideFile),
            new { topicId, chapterId, contentId });
        ViewData["SlideType"] = slideType;
        ViewData["SlideTitle"] = media.Title;

        return View();
    }

    [HttpGet]
    public async Task<IActionResult> SlideFile(
        int topicId,
        string? chapterId,
        int contentId,
        CancellationToken ct)
    {
        if (topicId <= 0 || contentId <= 0)
        {
            return NotFound();
        }

        var userId = User.Identity?.IsAuthenticated == true
            ? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            : null;

        if (!await _coursePurchaseService.CanAccessTopicAsync(userId, topicId, ct))
        {
            return User.Identity?.IsAuthenticated == true
                ? Forbid()
                : Challenge();
        }

        var notes = await _notesService.GetReaderAsync(topicId, chapterId, ct);
        var media = notes?.CurrentChapter?.Media.FirstOrDefault(x => x.ContentId == contentId);

        if (media is null || string.IsNullOrWhiteSpace(media.Slides))
        {
            return NotFound();
        }

        const string slidesPrefix = "/uploads/slides/";
        if (!media.Slides.StartsWith(slidesPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return NotFound();
        }

        var fileName = Path.GetFileName(media.Slides[slidesPrefix.Length..]);
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Contains("..", StringComparison.Ordinal))
        {
            return NotFound();
        }

        var path = Path.Combine(
            _environment.WebRootPath,
            "uploads",
            "slides",
            fileName);

        if (!System.IO.File.Exists(path))
        {
            return NotFound();
        }

        var contentType = Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".ppt" => "application/vnd.ms-powerpoint",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            _ => null
        };

        return contentType is null
            ? NotFound()
            : PhysicalFile(path, contentType, enableRangeProcessing: true);
    }
}
