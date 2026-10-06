using CIITStackLab.Application.Interfaces;
using CIITStackLab.WebApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CIITStackLab.WebApp.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,Super User")]
public class CourseTopicMappingController : Controller
{
    private readonly IAdminCourseTopicMappingService _mappingService;

    public CourseTopicMappingController(IAdminCourseTopicMappingService mappingService)
    {
        _mappingService = mappingService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        int? courseId = null,
        CancellationToken cancellationToken = default)
    {
        var page = await _mappingService.GetPageAsync(courseId, cancellationToken);

        return View(new AdminCourseTopicMappingViewModel
        {
            Courses = page.Courses,
            SelectedCourseId = page.SelectedCourseId,
            SelectedCourseName = page.SelectedCourseName,
            Topics = page.Topics,
            AssignedTopicCount = page.AssignedTopicCount
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(
        AdminCourseTopicMappingSaveModel model,
        CancellationToken cancellationToken)
    {
        var result = await _mappingService.SaveAsync(
            model.CourseId,
            model.TopicIds ?? new List<int>(),
            cancellationToken);

        TempData["AdminMappingMessage"] = result.Succeeded
            ? "Course-topic mapping saved successfully."
            : result.Error ?? "Unable to save course-topic mapping.";

        TempData["AdminMappingMessageType"] = result.Succeeded
            ? "success"
            : "error";

        return RedirectToAction(nameof(Index), new { courseId = model.CourseId });
    }
}
