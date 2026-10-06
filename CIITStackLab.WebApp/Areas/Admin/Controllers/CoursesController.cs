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

    public CoursesController(IAdminCourseService courseService)
    {
        _courseService = courseService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        int? editId = null,
        CancellationToken cancellationToken = default)
    {
        var activeCourses = await _courseService.GetActiveAsync(cancellationToken);
        var archivedCourses = await _courseService.GetArchivedAsync(cancellationToken);

        AdminCourseFormModel? editCourse = null;

        if (editId.HasValue)
        {
            var course = activeCourses.FirstOrDefault(x => x.Id == editId.Value);

            if (course is not null)
            {
                editCourse = new AdminCourseFormModel
                {
                    Id = course.Id,
                    CourseName = course.Title,
                    FeesAmount = course.FeesAmount,
                    FeesChangeDate = course.FeesChangeDate,
                    InstallmentPercentage = course.InstallmentPercentage
                };
            }
        }

        return View(new AdminCourseIndexViewModel
        {
            ActiveCourses = activeCourses,
            ArchivedCourses = archivedCourses,
            EditCourse = editCourse
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(
        AdminCourseFormModel model,
        CancellationToken cancellationToken)
    {
        model.CourseName = model.CourseName?.Trim() ?? string.Empty;

        if (!ModelState.IsValid)
        {
            TempData["AdminCourseMessage"] = "Please check the course details.";
            TempData["AdminCourseMessageType"] = "error";
            return RedirectToAction(nameof(Index), new { editId = model.Id == 0 ? (int?)null : model.Id });
        }

        var duplicate = await _courseService.CourseNameExistsAsync(
            model.CourseName,
            model.Id == 0 ? null : model.Id,
            cancellationToken);

        if (duplicate)
        {
            TempData["AdminCourseMessage"] = "A course with this name already exists.";
            TempData["AdminCourseMessageType"] = "error";
            return RedirectToAction(nameof(Index), new { editId = model.Id == 0 ? (int?)null : model.Id });
        }

        if (model.Id == 0)
        {
            var created = await _courseService.CreateAsync(
                model.CourseName,
                model.FeesAmount,
                model.FeesChangeDate,
                model.InstallmentPercentage,
                cancellationToken);

            return created.Succeeded
                ? RedirectToIndexWithMessage("Course created successfully.", "success")
                : RedirectToIndexWithMessage(created.Error ?? "Unable to create the course.", "error");
        }

        var updated = await _courseService.UpdateAsync(
            model.Id,
            model.CourseName,
            model.FeesAmount,
            model.FeesChangeDate,
            model.InstallmentPercentage,
            cancellationToken);

        return updated.Succeeded
            ? RedirectToIndexWithMessage("Course updated successfully.", "success")
            : RedirectToIndexWithMessage(updated.Error ?? "Unable to update the course.", "error", model.Id);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var result = await _courseService.DeleteAsync(id, cancellationToken);

        return result.Succeeded
            ? RedirectToIndexWithMessage("Course moved to archive.", "success")
            : RedirectToIndexWithMessage(result.Error ?? "Unable to archive the course.", "error");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(int id, CancellationToken cancellationToken)
    {
        var result = await _courseService.RestoreAsync(id, cancellationToken);

        return result.Succeeded
            ? RedirectToIndexWithMessage("Course restored successfully.", "success")
            : RedirectToIndexWithMessage(result.Error ?? "Unable to restore the course.", "error");
    }

    private IActionResult RedirectToIndexWithMessage(
        string message,
        string type,
        int? editId = null)
    {
        TempData["AdminCourseMessage"] = message;
        TempData["AdminCourseMessageType"] = type;

        return RedirectToAction(
            nameof(Index),
            editId.HasValue ? new { editId } : null)!;
    }
}
