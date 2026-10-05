using CIITStackLab.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CIITStackLab.WebApp.Controllers;

public class HomeController : Controller
{
    private readonly ICourseService _courseService;

    public HomeController(ICourseService courseService)
    {
        _courseService = courseService;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var courses = await _courseService.GetPublishedAsync(cancellationToken);
        return View(courses);
    }
}