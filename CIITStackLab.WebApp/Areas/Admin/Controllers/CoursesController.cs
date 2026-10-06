using CIITStackLab.Application.Interfaces;
using CIITStackLab.WebApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CIITStackLab.WebApp.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class CoursesController : Controller
{
    private readonly IAdminCourseService _courseService;

    public CoursesController(IAdminCourseService courseService)
    {
        _courseService = courseService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var model = new AdminCourseIndexViewModel
        {
            ActiveCourses = await _courseService.GetActiveAsync(cancellationToken),
            ArchivedCourses = await _courseService.GetArchivedAsync(cancellationToken)
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(AdminCourseFormModel model, CancellationToken cancellationToken)
    {
        model.CourseName = model.CourseName?.Trim() ?? string.Empty;

        if (!ModelState.IsValid)
        {
            return RedirectToIndexWithMessage("Please correct the course form and try again.", "error");
        }

        var duplicate = await _courseService.CourseNameExistsAsync(
            model.CourseName,
            model.Id == 0 ? null : model.Id,
            cancellationToken);

        if (duplicate)
        {
            return RedirectToIndexWithMessage(
                "A course with this name already exists.",
                "error",
                model.Id == 0 ? null : model.Id);
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
            : RedirectToIndexWithMessage(updated.Error ?? "Unable to update the course.", "error");
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

    private IActionResult RedirectToIndexWithMessage(string message, string type, int? editId = null)
    {
        TempData["AdminCourseMessage"] = message;
        TempData["AdminCourseMessageType"] = type;

        return RedirectToAction(nameof(Index), editId.HasValue
            ? new { editId }
            : null)!;
    }
}
