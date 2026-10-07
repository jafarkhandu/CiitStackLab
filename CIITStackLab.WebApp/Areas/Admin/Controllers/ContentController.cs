using CIITStackLab.Application.DTOs;
using CIITStackLab.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CIITStackLab.WebApp.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,Super User")]
public sealed class ContentController : Controller
{
    private readonly IAdminContentService _contentService;

    public ContentController(IAdminContentService contentService)
    {
        _contentService = contentService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        return View(await _contentService.GetIndexAsync(cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        await LoadCoursesAsync(cancellationToken);
        return View(new AdminContentInputDto());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AdminContentInputDto input, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await LoadFormDataAsync(input.CourseId, cancellationToken);
            return View(input);
        }

        var result = await _contentService.CreateAsync(input, cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            await LoadFormDataAsync(input.CourseId, cancellationToken);
            return View(input);
        }

        TempData["AdminContentMessage"] = "Content created successfully.";
        TempData["AdminContentMessageType"] = "success";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var content = await _contentService.GetByIdAsync(id, cancellationToken);
        if (content is null || content.Flag != 0) return NotFound();

        await LoadFormDataAsync(content.CourseId, cancellationToken);
        return View(new AdminContentInputDto
        {
            Id = content.Id,
            CourseId = content.CourseId,
            TopicId = content.TopicId,
            Title = content.Title,
            Slides = content.Slides,
            VideoName = content.VideoName
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, AdminContentInputDto input, CancellationToken cancellationToken)
    {
        input.Id = id;
        if (!ModelState.IsValid)
        {
            await LoadFormDataAsync(input.CourseId, cancellationToken);
            return View(input);
        }

        var result = await _contentService.UpdateAsync(id, input, cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            await LoadFormDataAsync(input.CourseId, cancellationToken);
            return View(input);
        }

        TempData["AdminContentMessage"] = "Content updated successfully.";
        TempData["AdminContentMessageType"] = "success";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var content = await _contentService.GetByIdAsync(id, cancellationToken);
        return content is null ? NotFound() : View(content);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var result = await _contentService.SoftDeleteAsync(id, cancellationToken);
        if (!result.Succeeded)
        {
            TempData["AdminContentMessage"] = result.Error;
            TempData["AdminContentMessageType"] = "error";
            return RedirectToAction(nameof(Index));
        }

        TempData["AdminContentMessage"] = "Content moved to archive.";
        TempData["AdminContentMessageType"] = "success";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(int id, CancellationToken cancellationToken)
    {
        var result = await _contentService.RestoreAsync(id, cancellationToken);
        if (!result.Succeeded)
        {
            TempData["AdminContentMessage"] = result.Error;
            TempData["AdminContentMessageType"] = "error";
            return RedirectToAction(nameof(Index));
        }

        TempData["AdminContentMessage"] = "Content restored successfully.";
        TempData["AdminContentMessageType"] = "success";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Topics(int courseId, CancellationToken cancellationToken)
    {
        if (courseId <= 0) return BadRequest();
        return Json(await _contentService.GetTopicsForCourseAsync(courseId, cancellationToken));
    }

    private async Task LoadCoursesAsync(CancellationToken cancellationToken)
    {
        var courses = await _contentService.GetCoursesAsync(cancellationToken);
        ViewBag.Courses = courses.Select(x => new SelectListItem(x.Title, x.Id.ToString())).ToList();
        ViewBag.Topics = new List<SelectListItem>();
    }

    private async Task LoadFormDataAsync(int courseId, CancellationToken cancellationToken)
    {
        await LoadCoursesAsync(cancellationToken);
        var topics = courseId > 0
            ? await _contentService.GetTopicsForCourseAsync(courseId, cancellationToken)
            : Array.Empty<AdminLookupDto>();
        ViewBag.Topics = topics.Select(x => new SelectListItem(x.Title, x.Id.ToString())).ToList();
    }
}
