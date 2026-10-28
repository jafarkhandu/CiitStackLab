using CIITStackLab.Application.DTOs;
using CIITStackLab.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CIITStackLab.WebApp.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,Super User")]
public sealed class NotesController : Controller
{
    private readonly INotesService _notesService;
    private readonly IVimeoVideoService _vimeoVideoService;

    public NotesController(
        INotesService notesService,
        IVimeoVideoService vimeoVideoService)
    {
        _notesService = notesService;
        _vimeoVideoService = vimeoVideoService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        return View(await _notesService.GetAdminIndexAsync(ct));
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        ViewBag.Options = await _notesService.GetTopicOptionsAsync(ct);
        return View(new AdminNoteInputDto());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        AdminNoteInputDto input,
        IFormFile? slidesFile,
        string? vimeoVideoUrl,
        bool modal = false,
        CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Options = await _notesService.GetTopicOptionsAsync(ct);
            return View(input);
        }

        try
        {
            await SaveUploadedFilesAsync(input, slidesFile, vimeoVideoUrl, ct);
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            ViewBag.Options = await _notesService.GetTopicOptionsAsync(ct);
            return View(input);
        }

        var result = await _notesService.CreateAsync(input, ct);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Unable to create note.");
            ViewBag.Options = await _notesService.GetTopicOptionsAsync(ct);
            return View(input);
        }

        if (modal) return ModalSaved();

        TempData["AdminNotesMessage"] = "Note and learning media created successfully.";
        TempData["AdminNotesMessageType"] = "success";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var note = await _notesService.GetAdminByIdAsync(id, ct);
        if (note is null)
        {
            return NotFound();
        }

        ViewBag.Options = await _notesService.GetTopicOptionsAsync(ct);
        return View(new AdminNoteInputDto
        {
            Id = note.Id,
            TopicId = note.TopicId,
            Title = note.Title,
            HtmlContent = note.HtmlContent,
            Slides = note.Slides,
            VideoName = note.VideoName
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        AdminNoteInputDto input,
        IFormFile? slidesFile,
        string? vimeoVideoUrl,
        bool modal = false,
        CancellationToken ct = default)
    {
        if (id <= 0)
        {
            return NotFound();
        }

        input.Id = id;
        var existing = await _notesService.GetAdminByIdAsync(id, ct);
        if (existing is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            ViewBag.Options = await _notesService.GetTopicOptionsAsync(ct);
            return View(input);
        }

        if (slidesFile is null || slidesFile.Length == 0)
        {
            input.Slides = existing.Slides;
        }

        if (string.IsNullOrWhiteSpace(vimeoVideoUrl))
        {
            input.VideoName = existing.VideoName;
        }

        try
        {
            await SaveUploadedFilesAsync(input, slidesFile, vimeoVideoUrl, ct);
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            ViewBag.Options = await _notesService.GetTopicOptionsAsync(ct);
            return View(input);
        }

        var result = await _notesService.UpdateAsync(id, input, ct);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Unable to update note.");
            ViewBag.Options = await _notesService.GetTopicOptionsAsync(ct);
            return View(input);
        }

        if (modal) return ModalSaved();

        TempData["AdminNotesMessage"] = "Note and learning media updated successfully.";
        TempData["AdminNotesMessageType"] = "success";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateVideoUpload(
        int contentId,
        string? fileName,
        long fileSize,
        string? title,
        CancellationToken cancellationToken)
    {
        try
        {
            string? existingVideoUrl = null;

            if (contentId > 0)
            {
                var existing = await _notesService.GetAdminByIdAsync(contentId, cancellationToken);
                if (existing is null)
                {
                    return NotFound(new { message = "The note record could not be found." });
                }

                existingVideoUrl = existing.VideoName;
            }

            var upload = await _vimeoVideoService.CreateDirectUploadAsync(
                fileName ?? string.Empty,
                fileSize,
                title ?? string.Empty,
                existingVideoUrl,
                cancellationToken);

            return Json(upload);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await _notesService.DeleteAsync(id, ct);
        TempData["AdminNotesMessage"] = result.Succeeded
            ? "Chapter deleted successfully."
            : result.Error ?? "Unable to delete chapter.";
        TempData["AdminNotesMessageType"] = result.Succeeded ? "success" : "error";
        return RedirectToAction(nameof(Index));
    }

    private ContentResult ModalSaved()
    {
        const string script = "<!doctype html><html><head><meta charset=\"utf-8\"></head><body><script>window.parent.postMessage({ type: 'ciit-content-saved' }, window.location.origin);</script></body></html>";
        return Content(script, "text/html");
    }

    private async Task SaveUploadedFilesAsync(
        AdminNoteInputDto input,
        IFormFile? slidesFile,
        string? vimeoVideoUrl,
        CancellationToken cancellationToken)
    {
        if (slidesFile is not null && slidesFile.Length > 0)
        {
            var extension = Path.GetExtension(slidesFile.FileName).ToLowerInvariant();
            if (extension != ".pdf")
            {
                throw new InvalidOperationException("Unsupported slides file type. Only PDF slides are allowed.");
            }

            var root = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "slides");
            Directory.CreateDirectory(root);
            var fileName = $"{Guid.NewGuid():N}{extension}";
            var path = Path.Combine(root, fileName);
            await using var stream = System.IO.File.Create(path);
            await slidesFile.CopyToAsync(stream, cancellationToken);
            input.Slides = $"/uploads/slides/{fileName}";
        }

        if (!string.IsNullOrWhiteSpace(vimeoVideoUrl))
        {
            input.VideoName = NormalizeVimeoPlayerUrl(vimeoVideoUrl);
        }
    }

    private static string NormalizeVimeoPlayerUrl(string value)
    {
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(uri.Host, "player.vimeo.com", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Invalid Vimeo video URL returned by the upload process.");
        }

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 2
            || !string.Equals(segments[0], "video", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(segments[1])
            || !segments[1].All(char.IsDigit))
        {
            throw new InvalidOperationException("Invalid Vimeo video URL returned by the upload process.");
        }

        return $"https://player.vimeo.com/video/{segments[1]}";
    }
}
