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
                var accessibleCourseIds = await _coursePurchaseService.GetAccessibleCourseIdsAsync(
                    userId,
                    cancellationToken);

                ViewBag.ShowLearningHub = accessibleCourseIds.Count > 0;
            }
        }

        var courses = await _courseService.GetPublishedAsync(cancellationToken);
        return View(courses);
    }
}
