using System.Security.Claims;
using System.Data.Common;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using CIITStackLab.Application.DTOs;
using CIITStackLab.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CIITStackLab.WebApp.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,Super User")]
public sealed class AssessmentsController : Controller
{
    private readonly IAdminMcqService _mcqService;
    private readonly IMcqImportService _mcqImportService;
    private readonly IAdminInterviewQuestionService _interviewQuestionService;
    private readonly IAdminPracticeProgramService _practiceProgramService;
    private readonly ILogger<AssessmentsController> _logger;

    public AssessmentsController(
        IAdminMcqService mcqService,
        IMcqImportService mcqImportService,
        IAdminInterviewQuestionService interviewQuestionService,
        IAdminPracticeProgramService practiceProgramService,
        ILogger<AssessmentsController> logger)
    {
        _mcqService = mcqService;
        _mcqImportService = mcqImportService;
        _interviewQuestionService = interviewQuestionService;
        _practiceProgramService = practiceProgramService;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Index() => View();

    [HttpGet]
    public async Task<IActionResult> Mcqs(
        string? previewToken,
        CancellationToken cancellationToken)
    {
        ViewBag.McqMessage = TempData["AdminMcqMessage"] as string;
        ViewBag.McqMessageType = TempData["AdminMcqMessageType"] as string;
        ViewBag.OpenMcqModal = TempData["OpenAdminMcqModal"] as string;

        if (string.Equals(
                ViewBag.OpenMcqModal as string,
                "import",
                StringComparison.OrdinalIgnoreCase)
            && string.Equals(
                ViewBag.McqMessageType as string,
                "error",
                StringComparison.OrdinalIgnoreCase))
        {
            ViewBag.McqImportMessage = ViewBag.McqMessage;
            ViewBag.McqImportMessageType = "error";
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!string.IsNullOrWhiteSpace(userId)
            && !string.IsNullOrWhiteSpace(previewToken))
        {
            ViewBag.McqImportPreview = _mcqImportService.GetPreview(
                previewToken,
                userId);

            if (ViewBag.McqImportPreview is null)
            {
                ViewBag.McqImportMessage =
                    "This preview has expired or is no longer available. Upload the file again to analyze it.";
                ViewBag.McqImportMessageType = "error";
            }
        }

        AdminMcqIndexDto model;

        try
        {
            model = await LoadMcqIndexAsync(cancellationToken);
        }
        catch (DbException)
        {
            model = new AdminMcqIndexDto();
            ViewBag.McqMessage =
                "The MCQ list could not be loaded from the database. Check the latest database setup script and the application connection string.";
            ViewBag.McqMessageType = "error";

            if (ViewBag.McqTopics is null)
            {
                try
                {
                    ViewBag.McqTopics = await _mcqService.GetTopicLookupAsync(cancellationToken);
                }
                catch (DbException)
                {
                    ViewBag.McqTopics = Array.Empty<AdminMcqTopicLookupDto>();
                }
            }
        }

        return View(model);
    }

    [HttpGet]
    public IActionResult CreateMcq()
    {
        TempData["OpenAdminMcqModal"] = "add";
        return RedirectToAction(nameof(Mcqs));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateMcq(
        AdminTopicMcqInputDto input,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var invalidModel = await LoadMcqIndexAsync(cancellationToken);
            ViewBag.AddMcqInput = input;
            ViewBag.OpenMcqModal = "add";
            return View("Mcqs", invalidModel);
        }

        (bool Succeeded, string? Error) result;

        try
        {
            result = await _mcqService.CreateForTopicAsync(input, cancellationToken);
        }
        catch (Exception exception) when (
            exception is DbException or DbUpdateException)
        {
            var failedModel = new AdminMcqIndexDto();

            try
            {
                ViewBag.McqTopics = await _mcqService.GetTopicLookupAsync(cancellationToken);
            }
            catch (Exception lookupException) when (
                lookupException is DbException or DbUpdateException)
            {
                ViewBag.McqTopics = Array.Empty<AdminMcqTopicLookupDto>();
            }

            ViewBag.AddMcqInput = input;
            ViewBag.AddMcqError =
                "The MCQ could not be saved to the database. Check that Database/20261012_AddTopicIdToContentQuestions.sql has been executed against the database used by this application, then try again.";
            ViewBag.OpenMcqModal = "add";

            return View("Mcqs", failedModel);
        }

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "The MCQ could not be created.");
            var failedModel = await LoadMcqIndexAsync(cancellationToken);
            ViewBag.AddMcqInput = input;
            ViewBag.OpenMcqModal = "add";
            return View("Mcqs", failedModel);
        }

        TempData["AdminMcqMessage"] = "MCQ created successfully for the selected chapter.";
        TempData["AdminMcqMessageType"] = "success";
        return RedirectToAction(nameof(Mcqs));
    }

    [HttpGet]
    public IActionResult BulkImportMcq()
    {
        TempData["OpenAdminMcqModal"] = "import";
        return RedirectToAction(nameof(Mcqs));
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 6 * 1024 * 1024)]
    public async Task<IActionResult> AnalyzeBulkMcq(
        int topicId,
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        Response.ContentType = "application/x-ndjson; charset=utf-8";
        Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
        Response.Headers["X-Accel-Buffering"] = "no";

        async Task WriteEventAsync(object payload, CancellationToken token)
        {
            await Response.WriteAsync(
                JsonSerializer.Serialize(payload) + "\n",
                token);
            await Response.Body.FlushAsync(token);
        }

        async Task ReportProgressAsync(string message, CancellationToken token)
        {
            await WriteEventAsync(
                new { type = "progress", message },
                token);
        }

        await Response.StartAsync(cancellationToken);
        await ReportProgressAsync(
            "Upload received. Preparing the chapter and file checks...",
            cancellationToken);

        var preview = new McqImportPreviewDto
        {
            TopicId = topicId,
            FileName = file is null ? string.Empty : Path.GetFileName(file.FileName)
        };

        try
        {
            if (file is null || file.Length == 0)
            {
                preview.FileErrors.Add("Choose a non-empty .txt or .docx file.");
            }
            else if (file.Length > 5 * 1024 * 1024)
            {
                preview.FileErrors.Add(
                    "The upload is larger than 5 MB. Split the file into smaller batches.");
            }
            else
            {
                await using var fileStream = file.OpenReadStream();

                preview = await _mcqImportService.PreviewAsync(
                    topicId,
                    file.FileName,
                    fileStream,
                    userId,
                    cancellationToken,
                    ReportProgressAsync);
            }
        }
        catch (DbException)
        {
            preview = new McqImportPreviewDto
            {
                TopicId = topicId,
                FileName = file is null ? string.Empty : Path.GetFileName(file.FileName)
            };
            preview.FileErrors.Add(
                "The database returned an error during analysis. Confirm Database/20261012_AddTopicIdToContentQuestions.sql has been executed against the database used by this application, then try again. No MCQs were saved.");
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(
                exception,
                "MCQ file analysis failed for chapter {TopicId}.",
                topicId);

            preview = new McqImportPreviewDto
            {
                TopicId = topicId,
                FileName = file is null ? string.Empty : Path.GetFileName(file.FileName)
            };
            preview.FileErrors.Add(
                "The file could not be analyzed. Verify that it is a valid .txt or .docx file and try again. No MCQs were saved.");
        }

        _mcqImportService.CachePreview(preview, userId);

        await WriteEventAsync(
            new
            {
                type = "result",
                previewToken = preview.PreviewToken,
                questionCount = preview.Questions.Count,
                validCount = preview.ValidQuestionCount,
                invalidCount = preview.InvalidQuestionCount,
                fileErrorCount = preview.FileErrors.Count,
                canImport = preview.CanImport
            },
            cancellationToken);

        return new EmptyResult();
    }

    [HttpGet]
    public IActionResult GetBulkMcqPreview(string? previewToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(previewToken))
        {
            return BadRequest();
        }

        var preview = _mcqImportService.GetPreview(previewToken, userId);

        return preview is null
            ? NotFound()
            : PartialView("_McqImportPreview", preview);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 6 * 1024 * 1024)]
    public async Task<IActionResult> PreviewBulkMcq(
        int topicId,
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        McqImportPreviewDto preview;

        try
        {
            if (file is null || file.Length == 0)
            {
                preview = new McqImportPreviewDto
                {
                    TopicId = topicId
                };
                preview.FileErrors.Add("Choose a non-empty .txt or .docx file.");
            }
            else if (file.Length > 5 * 1024 * 1024)
            {
                preview = new McqImportPreviewDto
                {
                    TopicId = topicId,
                    FileName = Path.GetFileName(file.FileName)
                };
                preview.FileErrors.Add(
                    "The upload is larger than 5 MB. Split the file into smaller batches.");
            }
            else
            {
                await using var fileStream = file.OpenReadStream();

                preview = await _mcqImportService.PreviewAsync(
                    topicId,
                    file.FileName,
                    fileStream,
                    userId,
                    cancellationToken);
            }
        }
        catch (DbException)
        {
            preview = new McqImportPreviewDto
            {
                TopicId = topicId,
                FileName = file is null ? string.Empty : Path.GetFileName(file.FileName)
            };
            preview.FileErrors.Add(
                "The database returned an error during analysis. No MCQs were saved. Confirm the topic_id setup SQL has been run against the database used by this application, then retry.");
        }

        // Keep the preview in server-side cache and redirect to the list page.
        // This prevents browser refresh from reposting the upload.
        _mcqImportService.CachePreview(preview, userId);
        TempData["OpenAdminMcqModal"] = "import";

        return RedirectToAction(
            nameof(Mcqs),
            new { previewToken = preview.PreviewToken });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmBulkMcq(
        string previewToken,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        McqImportOutcomeDto result;

        try
        {
            result = await _mcqImportService.ConfirmAsync(
                previewToken,
                userId,
                cancellationToken);
        }
        catch (DbException)
        {
            TempData["AdminMcqMessage"] =
                "The database returned an error during confirmation, so success could not be confirmed. Check the MCQ list before retrying. If the chapter column is missing, execute Database/20261012_AddTopicIdToContentQuestions.sql first.";
            TempData["AdminMcqMessageType"] = "error";
            TempData["OpenAdminMcqModal"] = "import";

            return RedirectToAction(
                nameof(Mcqs),
                new { previewToken });
        }

        if (result.Succeeded)
        {
            TempData["AdminMcqMessage"] = result.Message;
            TempData["AdminMcqMessageType"] = "success";
            return RedirectToAction(nameof(Mcqs));
        }

        TempData["AdminMcqMessage"] = result.Message;
        TempData["AdminMcqMessageType"] = "error";
        TempData["OpenAdminMcqModal"] = "import";

        if (result.Preview is not null)
        {
            _mcqImportService.CachePreview(result.Preview, userId);

            return RedirectToAction(
                nameof(Mcqs),
                new { previewToken = result.Preview.PreviewToken });
        }

        return RedirectToAction(nameof(Mcqs));
    }

    [HttpGet]
    public async Task<IActionResult> EditMcq(int id, CancellationToken cancellationToken)
    {
        var mcq = await _mcqService.GetByIdAsync(id, cancellationToken);
        if (mcq is null) return NotFound();

        await LoadMcqTopicLookupAsync(cancellationToken);
        return View(new AdminTopicMcqInputDto
        {
            TopicId = mcq.TopicId ?? 0,
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
    public async Task<IActionResult> EditMcq(int id, AdminTopicMcqInputDto input, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await LoadMcqTopicLookupAsync(cancellationToken);
            return View(input);
        }

        var result = await _mcqService.UpdateForTopicAsync(id, input, cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            await LoadMcqTopicLookupAsync(cancellationToken);
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

    private async Task<AdminMcqIndexDto> LoadMcqIndexAsync(
        CancellationToken cancellationToken)
    {
        ViewBag.McqTopics = await _mcqService.GetTopicLookupAsync(cancellationToken);
        return await _mcqService.GetIndexAsync(cancellationToken);
    }

    private async Task LoadMcqTopicLookupAsync(CancellationToken cancellationToken)
    {
        ViewBag.McqTopics = await _mcqService.GetTopicLookupAsync(cancellationToken);
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
