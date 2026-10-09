using CIITStackLab.Application.DTOs;
using CIITStackLab.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CIITStackLab.WebApp.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,Super User")]
public sealed class AssessmentsController : Controller
{
    private readonly IAdminMcqService _mcqService;

    public AssessmentsController(IAdminMcqService mcqService)
    {
        _mcqService = mcqService;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Mcqs(CancellationToken cancellationToken)
    {
        ViewBag.McqMessage = TempData["AdminMcqMessage"] as string;
        ViewBag.McqMessageType = TempData["AdminMcqMessageType"] as string;
        return View(await _mcqService.GetIndexAsync(cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> CreateMcq(CancellationToken cancellationToken)
    {
        await LoadMcqContentAsync(cancellationToken);
        return View(new AdminMcqInputDto());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateMcq(AdminMcqInputDto input, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await LoadMcqContentAsync(cancellationToken);
            return View(input);
        }

        var result = await _mcqService.CreateAsync(input, cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            await LoadMcqContentAsync(cancellationToken);
            return View(input);
        }

        TempData["AdminMcqMessage"] = "MCQ created successfully.";
        TempData["AdminMcqMessageType"] = "success";
        return RedirectToAction(nameof(Mcqs));
    }

    [HttpGet]
    public async Task<IActionResult> EditMcq(int id, CancellationToken cancellationToken)
    {
        var mcq = await _mcqService.GetByIdAsync(id, cancellationToken);
        if (mcq is null) return NotFound();

        await LoadMcqContentAsync(cancellationToken);
        return View(new AdminMcqInputDto
        {
            ContentId = mcq.ContentId ?? 0,
            Question = mcq.Question,
            Option1 = mcq.Option1,
            Option2 = mcq.Option2,
            Option3 = mcq.Option3,
            Option4 = mcq.Option4,
            CorrectOptionNumber = mcq.CorrectOptionNumber
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditMcq(int id, AdminMcqInputDto input, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await LoadMcqContentAsync(cancellationToken);
            return View(input);
        }

        var result = await _mcqService.UpdateAsync(id, input, cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            await LoadMcqContentAsync(cancellationToken);
            return View(input);
        }

        TempData["AdminMcqMessage"] = "MCQ updated successfully.";
        TempData["AdminMcqMessageType"] = "success";
        return RedirectToAction(nameof(Mcqs));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteMcq(int id, CancellationToken cancellationToken)
    {
        var result = await _mcqService.SoftDeleteAsync(id, cancellationToken);
        TempData["AdminMcqMessage"] = result.Succeeded ? "MCQ moved to archive." : result.Error;
        TempData["AdminMcqMessageType"] = result.Succeeded ? "success" : "error";
        return RedirectToAction(nameof(Mcqs));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RestoreMcq(int id, CancellationToken cancellationToken)
    {
        var result = await _mcqService.RestoreAsync(id, cancellationToken);
        TempData["AdminMcqMessage"] = result.Succeeded ? "MCQ restored successfully." : result.Error;
        TempData["AdminMcqMessageType"] = result.Succeeded ? "success" : "error";
        return RedirectToAction(nameof(Mcqs));
    }

    [HttpGet]
    public IActionResult InterviewQuestions()
    {
        return View();
    }

    [HttpGet]
    public IActionResult PracticePrograms()
    {
        return View();
    }

    private async Task LoadMcqContentAsync(CancellationToken cancellationToken)
    {
        var content = await _mcqService.GetContentLookupAsync(cancellationToken);
        ViewBag.McqContent = content
            .Select(x => new SelectListItem($"{x.TopicTitle} / {x.Title}", x.Id.ToString()))
            .ToList();
    }
}
