using CIITStackLab.Application.DTOs;
using CIITStackLab.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Http;

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
        await LoadTopicsAsync(cancellationToken);
        return View(new AdminContentInputDto());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AdminContentInputDto input, IFormFile? slidesFile, IFormFile? videoFile, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await LoadFormDataAsync(input.TopicId, cancellationToken);
            return View(input);
        }

        await SaveUploadedFilesAsync(input, slidesFile, videoFile, cancellationToken);
        var result = await _contentService.CreateAsync(input, cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            await LoadFormDataAsync(input.TopicId, cancellationToken);
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

        await LoadFormDataAsync(content.TopicId, cancellationToken);
        return View(new AdminContentInputDto
        {
            Id = content.Id,
            CourseId = content.CourseId,
            TopicId = content.TopicId,
            NoteId = content.NoteId ?? 0,
            Title = content.Title,
            Slides = content.Slides,
            VideoName = content.VideoName
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, AdminContentInputDto input, IFormFile? slidesFile, IFormFile? videoFile, CancellationToken cancellationToken)
    {
        input.Id = id;
        if (!ModelState.IsValid)
        {
            await LoadFormDataAsync(input.TopicId, cancellationToken);
            return View(input);
        }

        await SaveUploadedFilesAsync(input, slidesFile, videoFile, cancellationToken);
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
    public async Task<IActionResult> Notes(int topicId, CancellationToken cancellationToken)
    {
        if (topicId <= 0) return BadRequest();
        return Json(await _contentService.GetNotesForTopicAsync(topicId, cancellationToken));
    }

    private async Task LoadTopicsAsync(CancellationToken cancellationToken)
    {
        var topics = await _contentService.GetTopicsAsync(cancellationToken);
        ViewBag.Topics = topics.Select(x => new SelectListItem(x.Title, x.Id.ToString())).ToList();
        ViewBag.Notes = new List<SelectListItem>();
    }

    private async Task LoadFormDataAsync(int topicId, CancellationToken cancellationToken)
    {
        await LoadTopicsAsync(cancellationToken);
        if (topicId > 0)
        {
            var notes = await _contentService.GetNotesForTopicAsync(topicId, cancellationToken);
            ViewBag.Notes = notes.Select(x => new SelectListItem($"Chapter {x.SortOrder}: {x.Title}", x.Id.ToString())).ToList();
        }
    }

    private async Task SaveUploadedFilesAsync(AdminContentInputDto input, IFormFile? slidesFile, IFormFile? videoFile, CancellationToken cancellationToken)
    {
        if (slidesFile is not null && slidesFile.Length > 0)
            input.Slides = await SaveFileAsync(slidesFile, "slides", cancellationToken);
        if (videoFile is not null && videoFile.Length > 0)
            input.VideoName = await SaveFileAsync(videoFile, "videos", cancellationToken);
    }

    private async Task<string> SaveFileAsync(IFormFile file, string folder, CancellationToken cancellationToken)
    {
        var allowed = folder == "slides"
            ? new[] { ".pdf", ".ppt", ".pptx" }
            : new[] { ".mp4", ".webm", ".mov" };
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowed.Contains(extension)) throw new InvalidOperationException($"Unsupported {folder} file type.");
        var root = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", folder);
        Directory.CreateDirectory(root);
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var path = Path.Combine(root, fileName);
        await using var stream = System.IO.File.Create(path);
        await file.CopyToAsync(stream, cancellationToken);
        return $"/uploads/{folder}/{fileName}";
    }
}
