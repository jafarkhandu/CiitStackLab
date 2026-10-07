using CIITStackLab.Application.DTOs;
using CIITStackLab.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CIITStackLab.WebApp.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,Super User")]
public sealed class NotesController : Controller
{
    private readonly IAdminNotesService _notesService;

    public NotesController(IAdminNotesService notesService)
    {
        _notesService = notesService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var notes = await _notesService.GetIndexAsync(cancellationToken);
        return View(notes);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            return NotFound();
        }

        var note = await _notesService.GetByIdAsync(id, cancellationToken);
        return note is null ? NotFound() : View(note);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        AdminNotesInputDto input,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            return NotFound();
        }

        input.Id = id;

        if (!ModelState.IsValid)
        {
            var note = await _notesService.GetByIdAsync(id, cancellationToken);
            if (note is null)
            {
                return NotFound();
            }

            ViewData["HtmlContent"] = input.HtmlContent;
            return View(note);
        }

        var result = await _notesService.UpdateHtmlContentAsync(id, input, cancellationToken);

        if (!result.Succeeded)
        {
            TempData["AdminNotesMessage"] = result.Error ?? "Unable to save HTML content.";
            TempData["AdminNotesMessageType"] = "error";
            return RedirectToAction(nameof(Edit), new { id });
        }

        TempData["AdminNotesMessage"] = "HTML content saved successfully.";
        TempData["AdminNotesMessageType"] = "success";
        return RedirectToAction(nameof(Edit), new { id });
    }
}
