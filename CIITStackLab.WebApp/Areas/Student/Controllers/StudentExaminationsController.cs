using System.Security.Claims;
using CIITStackLab.Application.DTOs;
using CIITStackLab.Application.Interfaces;
using CIITStackLab.Domain.Entities;
using CIITStackLab.Infrastructure.Persistence;
using CIITStackLab.Infrastructure.Payments;
using CIITStackLab.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CIITStackLab.WebApp.Services;

namespace CIITStackLab.WebApp.Controllers;

[Authorize(Roles = "Student")]
[Route("Examinations")]
public sealed class StudentExaminationsController : Controller
{
    private readonly ITopicExaminationService _exams;
    private readonly ApplicationDbContext _db;
    private readonly ExamApplicationNotificationService _examApplicationNotificationService;
    private readonly PhonePePaymentService _phonePePaymentService;
    private readonly CoursePurchaseService _coursePurchaseService;
    private readonly ILogger<StudentExaminationsController> _logger;

    public StudentExaminationsController(
        ITopicExaminationService exams,
        ApplicationDbContext db,
        ExamApplicationNotificationService examApplicationNotificationService,
        PhonePePaymentService phonePePaymentService,
        CoursePurchaseService coursePurchaseService,
        ILogger<StudentExaminationsController> logger)
    {
        _exams = exams;
        _db = db;
        _examApplicationNotificationService = examApplicationNotificationService;
        _phonePePaymentService = phonePePaymentService;
        _coursePurchaseService = coursePurchaseService;
        _logger = logger;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();
        ViewBag.AvailableTopics = await (
            from cm in _db.CourseModules.AsNoTracking()
            join course in _db.Courses.AsNoTracking() on cm.CourseId equals course.Id
            join topic in _db.Topics.AsNoTracking() on cm.TopicId equals topic.Id
            where cm.Flag == 0 && course.Flag == 0 && topic.Flag == 0
                && (!course.FeesAmount.HasValue || course.FeesAmount <= 0 || _db.StudentCoursePurchases.Any(p => p.UserId == userId && p.CourseId == course.Id))
            orderby course.Title, topic.Title
            select new ExamTopicOption { CourseId = course.Id, CourseTitle = course.Title, TopicId = topic.Id, TopicTitle = topic.Title }
        ).ToListAsync(cancellationToken);

        var unusedPaidRetakeKeys = await _db.PaymentTransactions.AsNoTracking()
            .Where(payment => payment.UserId == userId
                && payment.PaymentPurpose == "ExamRetake"
                && payment.Status == "Completed"
                && payment.ExamAttemptId == null
                && payment.ExamTopicId.HasValue
                && payment.ExamAttemptNumber.HasValue)
            .Select(payment => payment.CourseId + ":" + payment.ExamTopicId!.Value + ":" + payment.ExamAttemptNumber!.Value)
            .ToListAsync(cancellationToken);
        ViewBag.UnusedPaidRetakeKeys = unusedPaidRetakeKeys;

        var pendingRetakeKeys = await _db.PaymentTransactions.AsNoTracking()
            .Where(payment => payment.UserId == userId
                && payment.PaymentPurpose == "ExamRetake"
                && (payment.Status == "Created" || payment.Status == "Pending")
                && payment.ExamTopicId.HasValue
                && payment.ExamAttemptNumber.HasValue)
            .Select(payment => payment.CourseId + ":" + payment.ExamTopicId!.Value + ":" + payment.ExamAttemptNumber!.Value)
            .ToListAsync(cancellationToken);
        ViewBag.PendingRetakeKeys = pendingRetakeKeys;

        return View(await _exams.GetStudentExamsAsync(userId, cancellationToken));
    }

    [HttpPost("Schedule")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Schedule(TopicExamScheduleRequest request, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();
        var result = await _exams.ScheduleAsync(userId, request.CourseId, request.TopicId,
            DateTime.SpecifyKind(request.ScheduledAtLocal.AddMinutes(request.TimezoneOffsetMinutes), DateTimeKind.Utc), cancellationToken);
        TempData[result.Succeeded ? "ExamMessage" : "ExamError"] =
            result.Succeeded ? "Exam scheduled successfully." : result.Error;
        if (!result.Succeeded)
        {
            await NotifyIfQuestionBankUnavailableAsync(
                userId,
                request.CourseId,
                request.TopicId,
                result.Error,
                cancellationToken);
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("StartNow")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartNow(TopicExamScheduleRequest request, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();
        var result = await _exams.ScheduleAsync(userId, request.CourseId, request.TopicId,
            DateTime.UtcNow, cancellationToken, startNow: true);
        if (!result.Succeeded || result.Exam is null)
        {
            TempData["ExamError"] = result.Error
                ?? "Exam questions are not available for this topic right now. Please try applying again later.";
            await NotifyIfQuestionBankUnavailableAsync(
                userId,
                request.CourseId,
                request.TopicId,
                result.Error,
                cancellationToken);
            return RedirectToAction("Learn", "Courses", new { id = request.CourseId, topicId = request.TopicId });
        }
        return RedirectToAction(nameof(Instructions), new { attemptId = result.Exam.AttemptId });
    }

    [HttpPost("ReExam")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReExam(int courseId, int topicId, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();

        var result = await _exams.ScheduleAsync(
            userId, courseId, topicId, DateTime.UtcNow, cancellationToken, startNow: true);

        if (!result.Succeeded || result.Exam is null)
        {
            TempData["ExamError"] = result.Error ?? "A new exam attempt could not be created.";
            await NotifyIfQuestionBankUnavailableAsync(
                userId,
                courseId,
                topicId,
                result.Error,
                cancellationToken);
            return RedirectToAction(nameof(Index));
        }

        return RedirectToAction(nameof(Instructions), new { attemptId = result.Exam.AttemptId });
    }

    [HttpPost("PayForReExam")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PayForReExam(int courseId, int topicId, CancellationToken cancellationToken)
    {
        var wantsJsonResponse = Request.Headers.Accept.Any(value => value.Contains("application/json", StringComparison.OrdinalIgnoreCase));
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();
        if (courseId <= 0 || topicId <= 0)
        {
            if (wantsJsonResponse) return NotFound(new { success = false, message = "The requested examination could not be found." });
            return NotFound();
        }

        IActionResult Error(string message, int statusCode = StatusCodes.Status400BadRequest)
        {
            if (wantsJsonResponse)
                return StatusCode(statusCode, new { success = false, message });

            TempData["ExamError"] = message;
            return RedirectToAction(nameof(Index));
        }

        var validTopic = await _db.CourseModules.AsNoTracking().AnyAsync(module =>
            module.CourseId == courseId && module.TopicId == topicId && module.Flag == 0
            && module.Course.Flag == 0 && module.Topic.Flag == 0, cancellationToken);
        if (!validTopic) return Error("The requested examination topic could not be found.", StatusCodes.Status404NotFound);

        if (!await _coursePurchaseService.CanAccessCourseAsync(userId, courseId, cancellationToken))
            return Error("You do not have access to this course.");

        if (await _exams.HasPassedTopicAsync(userId, courseId, topicId, cancellationToken))
            return Error("You have already passed this topic's examination.");

        var activeAttemptExists = await _db.TopicExamAttempts.AnyAsync(attempt =>
            attempt.UserId == userId && attempt.CourseId == courseId && attempt.TopicId == topicId
            && (attempt.Status == "Scheduled" || attempt.Status == "Ready" || attempt.Status == "InProgress"),
            cancellationToken);
        if (activeAttemptExists)
            return Error("An exam is already scheduled or active for this topic.");

        var questionAvailability = await _exams.CheckQuestionAvailabilityAsync(
            userId, courseId, topicId, cancellationToken);
        if (!questionAvailability.IsAvailable)
        {
            await NotifyIfQuestionBankUnavailableAsync(
                userId, courseId, topicId, questionAvailability.Error, cancellationToken);
            return Error(questionAvailability.Error
                ?? "Exam questions are not available for this topic right now.");
        }

        var attemptNumber = await _db.TopicExamAttempts.CountAsync(attempt =>
            attempt.UserId == userId && attempt.CourseId == courseId && attempt.TopicId == topicId,
            cancellationToken) + 1;
        if (attemptNumber <= 2)
            return Error("Your next two attempts are free. Start the re-exam from the examination records.");

        var existingUnusedPayment = await _db.PaymentTransactions.AnyAsync(payment =>
            payment.UserId == userId && payment.CourseId == courseId
            && payment.PaymentPurpose == "ExamRetake" && payment.ExamTopicId == topicId
            && payment.ExamAttemptNumber == attemptNumber
            && payment.Status == "Completed" && payment.ExamAttemptId == null,
            cancellationToken);
        if (existingUnusedPayment)
        {
            if (wantsJsonResponse)
                return Json(new { success = true, alreadyPaid = true, message = "Payment is already confirmed." });

            TempData["ExamMessage"] = "Payment is already confirmed. Start your re-exam from the exam records.";
            return RedirectToAction(nameof(Index));
        }

        var pendingPayment = await _db.PaymentTransactions.AnyAsync(payment =>
            payment.UserId == userId && payment.CourseId == courseId
            && payment.PaymentPurpose == "ExamRetake" && payment.ExamTopicId == topicId
            && payment.ExamAttemptNumber == attemptNumber
            && (payment.Status == "Created" || payment.Status == "Pending"),
            cancellationToken);
        if (pendingPayment)
            return Error("A payment order for this attempt is already pending. Complete or verify that order before creating another.");

        var baseFee = (attemptNumber - 2) * 100m;
        var gst = Math.Round(baseFee * 0.18m, 2, MidpointRounding.AwayFromZero);
        var totalAmount = baseFee + gst;
        var merchantOrderId = $"CIIT-EXAM-{Guid.NewGuid():N}";
        var transaction = new PaymentTransaction
        {
            UserId = userId,
            CourseId = courseId,
            PaymentPurpose = "ExamRetake",
            ExamTopicId = topicId,
            ExamAttemptNumber = attemptNumber,
            MerchantOrderId = merchantOrderId,
            Amount = totalAmount,
            Status = "Created",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.PaymentTransactions.Add(transaction);
        await _db.SaveChangesAsync(cancellationToken);

        var returnUrl = Url.Action(
            nameof(Index),
            "StudentExaminations",
            values: null,
            protocol: Request.Scheme,
            host: Request.Host.Value);
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            transaction.Status = "Failed";
            transaction.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            return Error("Payment could not be started. Please try again.");
        }

        try
        {
            var amountInPaise = checked((long)Math.Round(totalAmount * 100m, 0, MidpointRounding.AwayFromZero));
            var checkoutUrl = await _phonePePaymentService.CreateCheckoutAsync(merchantOrderId, amountInPaise, returnUrl);
            transaction.Status = "Pending";
            transaction.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);

            if (wantsJsonResponse)
                return Json(new { success = true, checkoutUrl, merchantOrderId, amount = totalAmount });

            return Redirect(checkoutUrl);
        }
        catch (Exception ex)
        {
            transaction.Status = ex is ArgumentException or InvalidOperationException ? "Failed" : "Pending";
            transaction.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(CancellationToken.None);
            _logger.LogError(ex, "Razorpay exam retake checkout could not be created for order {MerchantOrderId}.", merchantOrderId);

            var message = transaction.Status == "Pending"
                ? "The payment gateway response could not be confirmed. The order remains pending and no attempt has been unlocked."
                : "The payment could not be started. Please try again.";
            if (wantsJsonResponse)
                return StatusCode(transaction.Status == "Pending" ? StatusCodes.Status504GatewayTimeout : StatusCodes.Status502BadGateway,
                    new { success = false, status = transaction.Status, message });

            TempData["ExamError"] = message;
            return RedirectToAction(nameof(Index));
        }
    }

    private async Task NotifyIfQuestionBankUnavailableAsync(
        string userId,
        int courseId,
        int topicId,
        string? error,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(error)
            || !error.Contains("question", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var availability = await _exams.CheckQuestionAvailabilityAsync(
            userId,
            courseId,
            topicId,
            cancellationToken);
        if (availability.IsAvailable)
        {
            return;
        }

        await _examApplicationNotificationService.CreateMissingQuestionsNotificationsAsync(
            userId,
            User.Identity?.Name ?? userId,
            courseId,
            topicId,
            availability.ValidQuestionCount,
            error,
            cancellationToken);
    }

    [HttpGet("{attemptId:long}/Instructions")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> Instructions(long attemptId, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();
        var deviceToken = Request.Cookies[DeviceCookieName(attemptId)];
        if (string.IsNullOrWhiteSpace(deviceToken))
        {
            deviceToken = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
            Response.Cookies.Append(DeviceCookieName(attemptId), deviceToken, new CookieOptions
            {
                HttpOnly = true, Secure = Request.IsHttps, SameSite = SameSiteMode.Strict,
                Path = "/",
                IsEssential = true, Expires = DateTimeOffset.UtcNow.AddHours(3)
            });
        }
        var result = await _exams.OpenPreExamAsync(userId, attemptId, deviceToken, cancellationToken);
        if (!result.Succeeded || result.Session is null)
        {
            TempData["ExamError"] = result.Error;
            return RedirectToAction(nameof(Index));
        }
        return View("Instructions", result.Session);
    }

    [HttpPost("{attemptId:long}/Start")]
    [ValidateAntiForgeryToken]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> Start(long attemptId, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store, no-cache, max-age=0";
        Response.Headers.Pragma = "no-cache";
        Response.Headers.Expires = "0";

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();
        var deviceToken = Request.Cookies[DeviceCookieName(attemptId)] ?? string.Empty;
        var result = await _exams.StartAsync(userId, attemptId, deviceToken, cancellationToken);
        if (!result.Succeeded || result.Session is null)
        {
            TempData["ExamError"] = result.Error;
            return RedirectToAction(nameof(Index));
        }
        return View("TakeExam", result.Session);
    }

    [HttpPost("{attemptId:long}/Submit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(
        long attemptId,
        [FromForm] List<TopicExamAnswerInput> answers,
        [FromForm] bool finalSubmission,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();
        var deviceToken = Request.Cookies[DeviceCookieName(attemptId)] ?? string.Empty;
        var result = await _exams.SaveAnswersAsync(
            userId, attemptId, answers, deviceToken, cancellationToken, finalSubmission);
        if (!result.Succeeded) return BadRequest(new { error = result.Error });
        return Json(new { success = true, status = result.Result?.Status, score = result.Result?.Score, passed = result.Result?.Passed });
    }

    [HttpPost("{attemptId:long}/SecurityEvent")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SecurityEvent(long attemptId, TopicExamSecurityEventInput input, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();
        var deviceToken = Request.Cookies[DeviceCookieName(attemptId)] ?? string.Empty;
        var accepted = await _exams.RecordSecurityEventAsync(userId, attemptId, input.EventType, input.Details, deviceToken, cancellationToken);
        return accepted ? Ok() : BadRequest();
    }

    public sealed class ExamTopicOption
    {
        public int CourseId { get; init; }
        public string CourseTitle { get; init; } = string.Empty;
        public int TopicId { get; init; }
        public string TopicTitle { get; init; } = string.Empty;
    }

    private static string DeviceCookieName(long attemptId) => $"ciit-exam-device-{attemptId}";
}