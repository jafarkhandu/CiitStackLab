using CIITStackLab.Application.Interfaces;
using CIITStackLab.WebApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CIITStackLab.WebApp.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,Super User")]
public class CoursesController : Controller
{
    private const long MaxCourseImageSize = 5 * 1024 * 1024;
    private readonly IAdminCourseService _courseService;
    private readonly IWebHostEnvironment _environment;

    public CoursesController(IAdminCourseService courseService, IWebHostEnvironment environment)
    {
        _courseService = courseService;
        _environment = environment;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? editId = null, CancellationToken cancellationToken = default)
    {
        var activeCourses = await _courseService.GetActiveAsync(cancellationToken);
        var archivedCourses = await _courseService.GetArchivedAsync(cancellationToken);

        AdminCourseFormModel? editCourse = null;
        if (editId.HasValue)
        {
            var course = activeCourses.FirstOrDefault(x => x.Id == editId.Value);
            if (course is not null) editCourse = new AdminCourseFormModel { Id = course.Id, CourseName = course.Title, ImagePath = course.ImagePath, DiscountMode = course.DiscountMode, DiscountValue = course.DiscountValue };
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

        model.DiscountMode = string.IsNullOrWhiteSpace(model.DiscountMode) ? null : model.DiscountMode.Trim().ToLowerInvariant();
        if (model.DiscountMode is not null and not "percentage" and not "direct")
            return Fail("Choose a valid discount type.", model.Id);
        if (model.DiscountMode is not null && !model.DiscountValue.HasValue)
            return Fail("Enter a discount value.", model.Id);

        if (model.DiscountMode is not null)
        {
            var existingCourse = model.Id > 0
                ? (await _courseService.GetActiveAsync(cancellationToken))
                    .FirstOrDefault(item => item.Id == model.Id)
                : null;

            var currentTotalPrice = existingCourse?.TotalPrice ?? 0m;
            if (currentTotalPrice <= 0m)
                return Fail("A discount cannot be applied when the course total price is ₹0.00. Assign active topics with a price first.", model.Id);
        }
        if (model.DiscountMode == "percentage" && (model.DiscountValue < 0m || model.DiscountValue > 100m))
            return Fail("Percentage discount must be between 0 and 100.", model.Id);
        if (model.DiscountMode == "direct" && model.DiscountValue < 0m)
            return Fail("Discounted price cannot be negative.", model.Id);

        if (model.DiscountMode == "direct" && model.DiscountValue.HasValue)
        {
            var course = (await _courseService.GetActiveAsync(cancellationToken))
                .FirstOrDefault(item => item.Id == model.Id);
            if (course is not null && model.DiscountValue.Value > course.TotalPrice)
                return Fail("Discounted price cannot exceed the original course price.", model.Id);
        }

        if (model.DiscountMode is null) model.DiscountValue = null;

        if (await _courseService.CourseNameExistsAsync(model.CourseName, model.Id == 0 ? null : model.Id, cancellationToken))
            return Fail("A course with this name already exists.", model.Id);

        string? uploadedImagePath = null;
        if (model.ImageFile is { Length: > 0 })
        {
            var upload = await SaveCourseImageAsync(model.ImageFile, cancellationToken);
            if (!upload.Succeeded)
                return Fail(upload.Error ?? "The course image could not be uploaded.", model.Id);
            uploadedImagePath = upload.RelativePath;
        }

        if (model.Id == 0)
        {
            var created = await _courseService.CreateAsync(model.CourseName, uploadedImagePath, model.DiscountMode, model.DiscountValue, cancellationToken);
            if (!created.Succeeded)
            {
                DeleteCourseImage(uploadedImagePath);
                return Fail(created.Error ?? "Unable to create the course.");
            }
            return Success("Course created successfully.");
        }

        var oldImagePath = (await _courseService.GetActiveAsync(cancellationToken))
            .FirstOrDefault(x => x.Id == model.Id)?.ImagePath;
        var updated = await _courseService.UpdateAsync(model.Id, model.CourseName, uploadedImagePath, model.DiscountMode, model.DiscountValue, cancellationToken);
        if (!updated.Succeeded)
        {
            DeleteCourseImage(uploadedImagePath);
            return Fail(updated.Error ?? "Unable to update the course.", model.Id);
        }

        if (!string.IsNullOrWhiteSpace(uploadedImagePath))
            DeleteCourseImage(oldImagePath);

        return Success("Course updated successfully.");
    }

    private async Task<(bool Succeeded, string? RelativePath, string? Error)> SaveCourseImageAsync(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file.Length <= 0 || file.Length > MaxCourseImageSize)
            return (false, null, "Choose an image smaller than 5 MB.");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not (".jpg" or ".jpeg" or ".png" or ".webp"))
            return (false, null, "Only JPG, PNG, and WebP images are supported.");

        await using var input = file.OpenReadStream();
        var header = new byte[12];
        var bytesRead = await input.ReadAsync(header.AsMemory(0, header.Length), cancellationToken);
        if (!IsSupportedImage(header, bytesRead, extension))
            return (false, null, "The uploaded file is not a valid JPG, PNG, or WebP image.");

        var webRoot = _environment.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRoot))
            webRoot = Path.Combine(_environment.ContentRootPath, "wwwroot");

        var folder = Path.Combine(webRoot, "uploads", "courses");
        Directory.CreateDirectory(folder);
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(folder, fileName);
        input.Position = 0;
        await using (var output = System.IO.File.Create(fullPath))
            await input.CopyToAsync(output, cancellationToken);

        return (true, $"/uploads/courses/{fileName}", null);
    }

    private static bool IsSupportedImage(byte[] header, int length, string extension)
    {
        var isPng = length >= 8 && header[0] == 0x89 && header[1] == 0x50 &&
            header[2] == 0x4E && header[3] == 0x47 && header[4] == 0x0D &&
            header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A;
        var isJpeg = length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
        var isWebp = length >= 12 && header[0] == 0x52 && header[1] == 0x49 &&
            header[2] == 0x46 && header[3] == 0x46 && header[8] == 0x57 &&
            header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50;

        return extension switch
        {
            ".png" => isPng,
            ".jpg" or ".jpeg" => isJpeg,
            ".webp" => isWebp,
            _ => false
        };
    }

    private void DeleteCourseImage(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) ||
            !relativePath.StartsWith("/uploads/courses/", StringComparison.OrdinalIgnoreCase))
            return;

        var fileName = Path.GetFileName(relativePath);
        if (string.IsNullOrWhiteSpace(fileName))
            return;

        var webRoot = _environment.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRoot))
            webRoot = Path.Combine(_environment.ContentRootPath, "wwwroot");
        var fullPath = Path.Combine(webRoot, "uploads", "courses", fileName);
        if (System.IO.File.Exists(fullPath))
            System.IO.File.Delete(fullPath);
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
