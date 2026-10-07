using CIITStackLab.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CIITStackLab.WebApp.Controllers;

public sealed class NotesController : Controller
{
    private readonly INotesService _notesService;
    public NotesController(INotesService notesService) => _notesService = notesService;

    [HttpGet]
    public async Task<IActionResult> Index(int courseId,int topicId,string? pageId,CancellationToken ct)
    {
        if(courseId<=0||topicId<=0)return NotFound();
        var notes=await _notesService.GetReaderAsync(courseId,topicId,pageId,ct);
        return notes is null?NotFound():View(notes);
    }
}