using CIITStackLab.Application.DTOs;
using CIITStackLab.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CIITStackLab.WebApp.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,Super User")]
public sealed class NotesController : Controller
{
    private readonly INotesService _notesService;
    public NotesController(INotesService notesService) => _notesService = notesService;

    [HttpGet] public async Task<IActionResult> Index(CancellationToken ct) => View(await _notesService.GetAdminIndexAsync(ct));

    [HttpGet] public async Task<IActionResult> Create(CancellationToken ct)
    {
        ViewBag.Options = await _notesService.GetCourseTopicOptionsAsync(ct);
        return View(new AdminNoteInputDto { SortOrder = 1 });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AdminNoteInputDto input, CancellationToken ct)
    {
        if (!ModelState.IsValid) { ViewBag.Options=await _notesService.GetCourseTopicOptionsAsync(ct); return View(input); }
        var result=await _notesService.CreateAsync(input,ct);
        if(!result.Succeeded){ModelState.AddModelError("",result.Error??"Unable to create note.");ViewBag.Options=await _notesService.GetCourseTopicOptionsAsync(ct);return View(input);}
        TempData["AdminNotesMessage"]="Chapter created successfully."; TempData["AdminNotesMessageType"]="success";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet] public async Task<IActionResult> Edit(int id,CancellationToken ct)
    {
        var note=await _notesService.GetAdminByIdAsync(id,ct); if(note is null)return NotFound();
        ViewBag.Options=await _notesService.GetCourseTopicOptionsAsync(ct);
        return View(new AdminNoteInputDto {Id=note.Id,CourseId=note.CourseId,TopicId=note.TopicId,PageId=note.PageId,Title=note.Title,TitleWithNumber=note.TitleWithNumber,HtmlContent=note.HtmlContent,SortOrder=note.SortOrder});
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id,AdminNoteInputDto input,CancellationToken ct)
    {
        if(id<=0)return NotFound(); input.Id=id;
        if(!ModelState.IsValid){ViewBag.Options=await _notesService.GetCourseTopicOptionsAsync(ct);return View(input);}
        var result=await _notesService.UpdateAsync(id,input,ct);
        if(!result.Succeeded){ModelState.AddModelError("",result.Error??"Unable to update note.");ViewBag.Options=await _notesService.GetCourseTopicOptionsAsync(ct);return View(input);}
        TempData["AdminNotesMessage"]="Chapter updated successfully.";TempData["AdminNotesMessageType"]="success";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id,CancellationToken ct)
    {
        var result=await _notesService.DeleteAsync(id,ct);
        TempData["AdminNotesMessage"]=result.Succeeded?"Chapter deleted successfully.":result.Error??"Unable to delete chapter.";
        TempData["AdminNotesMessageType"]=result.Succeeded?"success":"error";
        return RedirectToAction(nameof(Index));
    }
}