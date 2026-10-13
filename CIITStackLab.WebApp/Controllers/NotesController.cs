using CIITStackLab.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CIITStackLab.WebApp.Controllers;

public sealed class NotesController : Controller
{
    private readonly INotesService _notesService;
    private readonly ICourseService _courseService;

    public NotesController(
        INotesService notesService,
        ICourseService courseService)
    {
        _notesService = notesService;
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
                ViewData["CourseId"] = course.Id.ToString();
                ViewData["CourseTitle"] = course.Title;
                ViewData["NotesTopicId"] = notes.TopicId.ToString();
                ViewData["NotesChapterId"] = notes.CurrentChapterId;
            }
        }

        return View(notes);
    }
}
