using CIITStackLab.Application.Interfaces;
using CIITStackLab.WebApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CIITStackLab.WebApp.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,Super User")]
public class CoursesController : Controller
{
    private readonly IAdminCourseService _courseService;
    public CoursesController(IAdminCourseService courseService) => _courseService = courseService;

    [HttpGet]
    public async Task<IActionResult> Index(int? editId = null, CancellationToken cancellationToken = default)
    {
        var activeCourses = await _courseService.GetActiveAsync(cancellationToken);
        var archivedCourses = await _courseService.GetArchivedAsync(cancellationToken);

        AdminCourseFormModel? editCourse = null;
        if (editId.HasValue)
        {
            var course = activeCourses.FirstOrDefault(x => x.Id == editId.Value);
            if (course is not null) editCourse = new AdminCourseFormModel { Id = course.Id, CourseName = course.Title };
        }

        return View(new AdminCourseIndexViewModel { ActiveCourses = activeCourses, ArchivedCourses = archivedCourses, EditCourse = editCourse });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(AdminCourseFormModel model, CancellationToken cancellationToken)
    {
        model.CourseName = model.CourseName?.Trim() ?? string.Empty;

        if (!ModelState.IsValid)
            return Fail("Please check the course details.", model.Id);

        if (await _courseService.CourseNameExistsAsync(model.CourseName, model.Id == 0 ? null : model.Id, cancellationToken))
            return Fail("A course with this name already exists.", model.Id);

        if (model.Id == 0)
        {
            var created = await _courseService.CreateAsync(model.CourseName, cancellationToken);
            return created.Succeeded ? Success("Course created successfully.") : Fail(created.Error ?? "Unable to create the course.");
        }

        var updated = await _courseService.UpdateAsync(model.Id, model.CourseName, cancellationToken);
        return updated.Succeeded ? Success("Course updated successfully.") : Fail(updated.Error ?? "Unable to update the course.", model.Id);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
        => Result(await _courseService.DeleteAsync(id, cancellationToken), "Course moved to archive.", "Unable to archive the course.");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(int id, CancellationToken cancellationToken)
        => Result(await _courseService.RestoreAsync(id, cancellationToken), "Course restored successfully.", "Unable to restore the course.");

    private IActionResult Success(string message, int? editId = null)
    {
        TempData["AdminCourseMessage"] = message;
        TempData["AdminCourseMessageType"] = "success";
        return RedirectToAction(nameof(Index), editId.HasValue ? new { editId } : null)!;
    }

    private IActionResult Fail(string message, int? editId = null)
    {
        TempData["AdminCourseMessage"] = message;
        TempData["AdminCourseMessageType"] = "error";
        return RedirectToAction(nameof(Index), editId.HasValue ? new { editId } : null)!;
    }

    private IActionResult Result((bool Succeeded, string? Error) result, string success, string failure)
        => result.Succeeded ? Success(success) : Fail(result.Error ?? failure);
}
