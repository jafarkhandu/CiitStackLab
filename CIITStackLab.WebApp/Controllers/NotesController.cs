using CIITStackLab.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CIITStackLab.WebApp.Controllers;

public sealed class NotesController : Controller
{
    private readonly INotesService _notesService;

    public NotesController(INotesService notesService)
    {
        _notesService = notesService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        int topicId,
        string? chapterId,
        CancellationToken ct)
    {
        if (topicId <= 0)
        {
            return NotFound();
        }

        var notes = await _notesService.GetReaderAsync(
            topicId,
            chapterId,
            ct);

        return notes is null
            ? NotFound()
            : View(notes);
    }
}
