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

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Student")]
    [HttpGet]
    public async Task<IActionResult> Dashboard(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        var activeCourseIds = await _coursePurchaseService.GetAccessibleCourseIdsAsync(
            userId,
            cancellationToken);
        var publishedCourses = await _courseService.GetPublishedAsync(cancellationToken);
        var activeCourseIdSet = activeCourseIds.ToHashSet();
        var courses = publishedCourses
            .Where(course => activeCourseIdSet.Contains(course.Id))
            .OrderBy(course => course.Title)
            .ToList();

        return View("StudentDashboard", courses);
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        ViewBag.ShowLearningHub = User.IsInRole("Student");

        var courses = await _courseService.GetPublishedAsync(cancellationToken);
        return View(courses);
    }
}
