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
    private readonly IAdminInterviewQuestionService _interviewQuestionService;
    private readonly IAdminPracticeProgramService _practiceProgramService;

    public AssessmentsController(
        IAdminMcqService mcqService,
        IAdminInterviewQuestionService interviewQuestionService,
        IAdminPracticeProgramService practiceProgramService)
    {
        _mcqService = mcqService;
        _interviewQuestionService = interviewQuestionService;
        _practiceProgramService = practiceProgramService;
    }

    [HttpGet]
    public IActionResult Index() => View();

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
    public async Task<IActionResult> InterviewQuestions(CancellationToken cancellationToken)
    {
        ViewBag.InterviewMessage = TempData["AdminInterviewMessage"] as string;
        ViewBag.InterviewMessageType = TempData["AdminInterviewMessageType"] as string;
        return View(await _interviewQuestionService.GetIndexAsync(cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> CreateInterviewQuestion(CancellationToken cancellationToken)
    {
        await LoadInterviewQuestionContentAsync(cancellationToken);
        return View(new AdminInterviewQuestionInputDto());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateInterviewQuestion(AdminInterviewQuestionInputDto input, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await LoadInterviewQuestionContentAsync(cancellationToken);
            return View(input);
        }

        var result = await _interviewQuestionService.CreateAsync(input, cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            await LoadInterviewQuestionContentAsync(cancellationToken);
            return View(input);
        }

        TempData["AdminInterviewMessage"] = "Interview question created successfully.";
        TempData["AdminInterviewMessageType"] = "success";
        return RedirectToAction(nameof(InterviewQuestions));
    }

    [HttpGet]
    public async Task<IActionResult> EditInterviewQuestion(int id, CancellationToken cancellationToken)
    {
        var question = await _interviewQuestionService.GetByIdAsync(id, cancellationToken);
        if (question is null) return NotFound();

        await LoadInterviewQuestionContentAsync(cancellationToken);
        return View(new AdminInterviewQuestionInputDto
        {
            ContentId = question.ContentId ?? 0,
            Question = question.Question,
            Answer = question.Answer
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditInterviewQuestion(int id, AdminInterviewQuestionInputDto input, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await LoadInterviewQuestionContentAsync(cancellationToken);
            return View(input);
        }

        var result = await _interviewQuestionService.UpdateAsync(id, input, cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            await LoadInterviewQuestionContentAsync(cancellationToken);
            return View(input);
        }

        TempData["AdminInterviewMessage"] = "Interview question updated successfully.";
        TempData["AdminInterviewMessageType"] = "success";
        return RedirectToAction(nameof(InterviewQuestions));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteInterviewQuestion(int id, CancellationToken cancellationToken)
    {
        var result = await _interviewQuestionService.SoftDeleteAsync(id, cancellationToken);
        TempData["AdminInterviewMessage"] = result.Succeeded ? "Interview question moved to archive." : result.Error;
        TempData["AdminInterviewMessageType"] = result.Succeeded ? "success" : "error";
        return RedirectToAction(nameof(InterviewQuestions));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RestoreInterviewQuestion(int id, CancellationToken cancellationToken)
    {
        var result = await _interviewQuestionService.RestoreAsync(id, cancellationToken);
        TempData["AdminInterviewMessage"] = result.Succeeded ? "Interview question restored successfully." : result.Error;
        TempData["AdminInterviewMessageType"] = result.Succeeded ? "success" : "error";
        return RedirectToAction(nameof(InterviewQuestions));
    }

    [HttpGet]
    public async Task<IActionResult> PracticePrograms(CancellationToken cancellationToken)
    {
        ViewBag.PracticeMessage = TempData["AdminPracticeMessage"] as string;
        ViewBag.PracticeMessageType = TempData["AdminPracticeMessageType"] as string;
        return View(await _practiceProgramService.GetIndexAsync(cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> CreatePracticeProgram(CancellationToken cancellationToken)
    {
        await LoadPracticeProgramContentAsync(cancellationToken);
        return View(new AdminPracticeProgramInputDto());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreatePracticeProgram(AdminPracticeProgramInputDto input, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await LoadPracticeProgramContentAsync(cancellationToken);
            return View(input);
        }

        var result = await _practiceProgramService.CreateAsync(input, cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            await LoadPracticeProgramContentAsync(cancellationToken);
            return View(input);
        }

        TempData["AdminPracticeMessage"] = "Practice program created successfully.";
        TempData["AdminPracticeMessageType"] = "success";
        return RedirectToAction(nameof(PracticePrograms));
    }

    [HttpGet]
    public async Task<IActionResult> EditPracticeProgram(int id, CancellationToken cancellationToken)
    {
        var program = await _practiceProgramService.GetByIdAsync(id, cancellationToken);
        if (program is null) return NotFound();

        await LoadPracticeProgramContentAsync(cancellationToken);
        return View(new AdminPracticeProgramInputDto
        {
            ContentId = program.ContentId ?? 0,
            QuestionTitle = program.QuestionTitle,
            QuestionDescription = program.QuestionDescription
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditPracticeProgram(int id, AdminPracticeProgramInputDto input, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await LoadPracticeProgramContentAsync(cancellationToken);
            return View(input);
        }

        var result = await _practiceProgramService.UpdateAsync(id, input, cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            await LoadPracticeProgramContentAsync(cancellationToken);
            return View(input);
        }

        TempData["AdminPracticeMessage"] = "Practice program updated successfully.";
        TempData["AdminPracticeMessageType"] = "success";
        return RedirectToAction(nameof(PracticePrograms));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePracticeProgram(int id, CancellationToken cancellationToken)
    {
        var result = await _practiceProgramService.SoftDeleteAsync(id, cancellationToken);
        TempData["AdminPracticeMessage"] = result.Succeeded ? "Practice program moved to archive." : result.Error;
        TempData["AdminPracticeMessageType"] = result.Succeeded ? "success" : "error";
        return RedirectToAction(nameof(PracticePrograms));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RestorePracticeProgram(int id, CancellationToken cancellationToken)
    {
        var result = await _practiceProgramService.RestoreAsync(id, cancellationToken);
        TempData["AdminPracticeMessage"] = result.Succeeded ? "Practice program restored successfully." : result.Error;
        TempData["AdminPracticeMessageType"] = result.Succeeded ? "success" : "error";
        return RedirectToAction(nameof(PracticePrograms));
    }

    [HttpGet]
    public async Task<IActionResult> CreatePracticeAnswer(CancellationToken cancellationToken)
    {
        await LoadPracticeProgramLookupAsync(cancellationToken);
        return View(new AdminPracticeProgramAnswerInputDto());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreatePracticeAnswer(AdminPracticeProgramAnswerInputDto input, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await LoadPracticeProgramLookupAsync(cancellationToken);
            return View(input);
        }

        var result = await _practiceProgramService.CreateAnswerAsync(input, cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            await LoadPracticeProgramLookupAsync(cancellationToken);
            return View(input);
        }

        TempData["AdminPracticeMessage"] = "Practice program solution added successfully.";
        TempData["AdminPracticeMessageType"] = "success";
        return RedirectToAction(nameof(PracticePrograms));
    }

    [HttpGet]
    public async Task<IActionResult> EditPracticeAnswer(int id, CancellationToken cancellationToken)
    {
        var answer = await _practiceProgramService.GetAnswerByIdAsync(id, cancellationToken);
        if (answer is null) return NotFound();

        await LoadPracticeProgramLookupAsync(cancellationToken);
        return View(new AdminPracticeProgramAnswerInputDto
        {
            ProgramQuestionId = answer.ProgramQuestionId ?? 0,
            Answer = answer.Answer,
            Description = answer.Description
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditPracticeAnswer(int id, AdminPracticeProgramAnswerInputDto input, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await LoadPracticeProgramLookupAsync(cancellationToken);
            return View(input);
        }

        var result = await _practiceProgramService.UpdateAnswerAsync(id, input, cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            await LoadPracticeProgramLookupAsync(cancellationToken);
            return View(input);
        }

        TempData["AdminPracticeMessage"] = "Practice program solution updated successfully.";
        TempData["AdminPracticeMessageType"] = "success";
        return RedirectToAction(nameof(PracticePrograms));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePracticeAnswer(int id, CancellationToken cancellationToken)
    {
        var result = await _practiceProgramService.SoftDeleteAnswerAsync(id, cancellationToken);
        TempData["AdminPracticeMessage"] = result.Succeeded ? "Practice program solution moved to archive." : result.Error;
        TempData["AdminPracticeMessageType"] = result.Succeeded ? "success" : "error";
        return RedirectToAction(nameof(PracticePrograms));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RestorePracticeAnswer(int id, CancellationToken cancellationToken)
    {
        var result = await _practiceProgramService.RestoreAnswerAsync(id, cancellationToken);
        TempData["AdminPracticeMessage"] = result.Succeeded ? "Practice program solution restored successfully." : result.Error;
        TempData["AdminPracticeMessageType"] = result.Succeeded ? "success" : "error";
        return RedirectToAction(nameof(PracticePrograms));
    }

    private async Task LoadMcqContentAsync(CancellationToken cancellationToken)
    {
        var content = await _mcqService.GetContentLookupAsync(cancellationToken);
        ViewBag.McqContent = content.Select(x => new SelectListItem($"{x.TopicTitle} / {x.Title}", x.Id.ToString())).ToList();
    }

    private async Task LoadInterviewQuestionContentAsync(CancellationToken cancellationToken)
    {
        var content = await _interviewQuestionService.GetContentLookupAsync(cancellationToken);
        ViewBag.InterviewQuestionContent = content.Select(x => new SelectListItem($"{x.TopicTitle} / {x.Title}", x.Id.ToString())).ToList();
    }

    private async Task LoadPracticeProgramContentAsync(CancellationToken cancellationToken)
    {
        var content = await _practiceProgramService.GetContentLookupAsync(cancellationToken);
        ViewBag.PracticeProgramContent = content.Select(x => new SelectListItem($"{x.TopicTitle} / {x.Title}", x.Id.ToString())).ToList();
    }

    private async Task LoadPracticeProgramLookupAsync(CancellationToken cancellationToken)
    {
        var programs = await _practiceProgramService.GetProgramLookupAsync(cancellationToken);
        ViewBag.PracticePrograms = programs.Select(x => new SelectListItem(x.QuestionTitle, x.Id.ToString())).ToList();
    }
}
