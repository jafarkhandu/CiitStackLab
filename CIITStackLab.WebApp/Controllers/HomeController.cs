using System.Security.Claims;
using CIITStackLab.Application.Interfaces;
using CIITStackLab.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace CIITStackLab.WebApp.Controllers;

public class HomeController : Controller
{
    private readonly ICourseService _courseService;
    private readonly CoursePurchaseService _coursePurchaseService;

    public HomeController(
        ICourseService courseService,
        CoursePurchaseService coursePurchaseService)
    {
        _courseService = courseService;
        _coursePurchaseService = coursePurchaseService;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        ViewBag.ShowLearningHub = false;

        if (User.IsInRole("Student"))
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!string.IsNullOrWhiteSpace(userId))
            {
                ViewBag.ShowLearningHub = await _coursePurchaseService.HasAnyActivePurchaseAsync(
                    userId,
                    cancellationToken);
            }
        }

        var courses = await _courseService.GetPublishedAsync(cancellationToken);
        return View(courses);
    }
}
