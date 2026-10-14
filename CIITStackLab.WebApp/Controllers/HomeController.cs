using System.Security.Claims;
using CIITStackLab.Application.Interfaces;
using CIITStackLab.Infrastructure.Services;
using CIITStackLab.Infrastructure.Persistence;
using CIITStackLab.WebApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

namespace CIITStackLab.WebApp.Controllers;

public class HomeController : Controller
{
    private readonly ICourseService _courseService;
    private readonly CoursePurchaseService _coursePurchaseService;
    private readonly ApplicationDbContext _dbContext;

    public HomeController(
        ICourseService courseService,
        CoursePurchaseService coursePurchaseService,
        ApplicationDbContext dbContext)
    {
        _courseService = courseService;
        _coursePurchaseService = coursePurchaseService;
        _dbContext = dbContext;
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

        var progressRecords = await _dbContext.StudentLessonProgress
            .AsNoTracking()
            .Where(progress => progress.UserId == userId && activeCourseIdSet.Contains(progress.CourseId))
            .OrderByDescending(progress => progress.LastAccessedAt)
            .ToListAsync(cancellationToken);

        var courseTitles = courses.ToDictionary(course => course.Id, course => course.Title);
        var recentActivity = progressRecords
            .Take(5)
            .Select(progress => new StudentLearningActivityViewModel
            {
                CourseTitle = courseTitles.TryGetValue(progress.CourseId, out var title)
                    ? title
                    : "Course",
                ContentId = progress.ContentId,
                IsCompleted = progress.IsCompleted,
                LastAccessedAt = progress.LastAccessedAt
            })
            .ToList();

        var model = new StudentLearningDashboardViewModel
        {
            Courses = courses,
            CompletedLessons = progressRecords.Count(progress => progress.IsCompleted),
            TrackedLessons = progressRecords.Count,
            RecentActivity = recentActivity,
            MostRecentCourseTitle = progressRecords.Count > 0
                && courseTitles.TryGetValue(progressRecords[0].CourseId, out var recentTitle)
                    ? recentTitle
                    : null,
            MostRecentActivityAt = progressRecords.FirstOrDefault()?.LastAccessedAt
        };

        return View("StudentDashboard", model);
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        ViewBag.ShowLearningHub = User.IsInRole("Student");

        var courses = await _courseService.GetPublishedAsync(cancellationToken);
        return View(courses);
    }
}
