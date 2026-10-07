using CIITStackLab.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CIITStackLab.WebApp.Controllers;

public class CoursesController : Controller
{
    private readonly ICourseService _courseService;

    public CoursesController(ICourseService courseService)
    {
        _courseService = courseService;
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            return NotFound();
        }

        var course = await _courseService.GetDetailsAsync(id, cancellationToken);

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

        var course = await _courseService.GetLearningAsync(
            id,
            contentId,
            cancellationToken);

        return course is null
            ? NotFound()
            : View(course);
    }
}
