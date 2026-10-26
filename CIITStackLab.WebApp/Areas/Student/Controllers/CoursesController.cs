using System.Security.Claims;
using CIITStackLab.Application.DTOs;
using CIITStackLab.Application.Interfaces;
using CIITStackLab.Domain.Entities;
using CIITStackLab.Infrastructure.Services;
using CIITStackLab.Infrastructure.Payments;
using CIITStackLab.Infrastructure.Persistence;
using CIITStackLab.WebApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CIITStackLab.WebApp.Services;

namespace CIITStackLab.WebApp.Controllers;

public class CoursesController : Controller
{
    private readonly ICourseService _courseService;
    private readonly IStudentProgressService _studentProgressService;
    private readonly ICourseAssessmentService _courseAssessmentService;
    private readonly CoursePurchaseService _coursePurchaseService;
    private readonly IMuxVideoService _muxVideoService;
    private readonly ApplicationDbContext _dbContext;
    private readonly ITopicExaminationService _topicExaminationService;
    private readonly IEmailService _emailService;
    private readonly PhonePePaymentService _phonePePaymentService;
    private readonly PaymentNotificationService _paymentNotificationService;
    private readonly ILogger<CoursesController> _logger;
    private readonly ExamApplicationNotificationService _examApplicationNotificationService;

    public CoursesController(
        ICourseService courseService,
        IStudentProgressService studentProgressService,
        ICourseAssessmentService courseAssessmentService,
        CoursePurchaseService coursePurchaseService,
        IMuxVideoService muxVideoService,
        ApplicationDbContext dbContext,
        ITopicExaminationService topicExaminationService,
        IEmailService emailService,
        PhonePePaymentService phonePePaymentService,
        PaymentNotificationService paymentNotificationService,
        ILogger<CoursesController> logger,
        ExamApplicationNotificationService examApplicationNotificationService)
    {
        _courseService = courseService;
        _studentProgressService = studentProgressService;
        _courseAssessmentService = courseAssessmentService;
        _coursePurchaseService = coursePurchaseService;
        _muxVideoService = muxVideoService;
        _dbContext = dbContext;
        _topicExaminationService = topicExaminationService;
        _emailService = emailService;
        _phonePePaymentService = phonePePaymentService;
        _paymentNotificationService = paymentNotificationService;
        _logger = logger;
        _examApplicationNotificationService = examApplicationNotificationService;
    }

    [HttpGet]
    public async Task<IActionResult> Details(
        int id,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            return NotFound();
        }

        var course = await _courseService.GetDetailsAsync(id, cancellationToken);

        if (course is null)
        {
            return NotFound();
        }

        course.IsStudent = User.IsInRole("Student");
        var userId = course.IsStudent ? User.FindFirstValue(ClaimTypes.NameIdentifier) : null;
        course.HasAccess = await _coursePurchaseService.CanAccessCourseAsync(userId, id, cancellationToken);

        return View(course);
    }

    [HttpPost("Courses/DemoPayment/{id:int}")]
    [Authorize(Roles = "Student")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DemoPayment(int id, CancellationToken cancellationToken)
    {
        var wantsJsonResponse = Request.Headers.Accept.Any(value => value.Contains("application/json", StringComparison.OrdinalIgnoreCase));
        if (id <= 0) return NotFound();
        var course = await _courseService.GetByIdAsync(id, cancellationToken);
        if (course is null) return NotFound();
        var price = await _coursePurchaseService.GetCoursePriceAsync(id, cancellationToken);
        if (price <= 0m)
        {
            if (wantsJsonResponse) return Json(new { redirectUrl = Url.Action(nameof(CourseHome), "Courses") });
            return RedirectToAction(nameof(CourseHome));
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();
        if (await _coursePurchaseService.HasPurchasedCourseAsync(userId, id, cancellationToken))
        {
            if (wantsJsonResponse) return Json(new { redirectUrl = Url.Action(nameof(CourseHome), "Courses") });
            return RedirectToAction(nameof(CourseHome));
        }

        var merchantOrderId = $"CIIT-{Guid.NewGuid():N}";
        var transaction = new PaymentTransaction
        {
            UserId = userId,
            CourseId = id,
            MerchantOrderId = merchantOrderId,
            Amount = price,
            Status = "Created",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.PaymentTransactions.Add(transaction);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var redirectUrl = Url.Action(
            nameof(PhonePeReturn),
            "Courses",
            new { merchantOrderId },
            Request.Scheme,
            Request.Host.Value);

        if (string.IsNullOrWhiteSpace(redirectUrl))
        {
            transaction.Status = "Failed";
            transaction.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
            if (wantsJsonResponse) return BadRequest(new { message = "Payment could not be started. Please try again." });
            TempData["CoursePaymentMessage"] = "Payment could not be started. Please try again.";
            return RedirectToAction(nameof(Details), new { id });
        }

        try
        {
            var amountInPaise = checked((long)Math.Round(price * 100m, 0, MidpointRounding.AwayFromZero));
            var checkoutUrl = await _phonePePaymentService.CreateCheckoutAsync(merchantOrderId, amountInPaise, redirectUrl);
            transaction.Status = "Pending";
            transaction.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            if (wantsJsonResponse) return Json(new { checkoutUrl, merchantOrderId });
            return Redirect(checkoutUrl);
        }
        catch (TimeoutException ex)
        {
            transaction.Status = "Pending";
            transaction.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(CancellationToken.None);
            _logger.LogWarning(ex, "PhonePe checkout creation timed out for merchant order {MerchantOrderId}.", merchantOrderId);
            var message = "The payment gateway is taking too long to respond. Your payment order is still pending and has not granted course access.";
            if (wantsJsonResponse) return StatusCode(StatusCodes.Status504GatewayTimeout, new { message });
            TempData["CoursePaymentMessage"] = message;
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            transaction.Status = "Pending";
            transaction.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(CancellationToken.None);
            _logger.LogWarning(ex, "PhonePe checkout creation was cancelled by the gateway for merchant order {MerchantOrderId}.", merchantOrderId);
            var message = "The payment gateway connection was interrupted. Your payment order remains pending and will not grant access until verified.";
            if (wantsJsonResponse) return StatusCode(StatusCodes.Status504GatewayTimeout, new { message });
            TempData["CoursePaymentMessage"] = message;
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            transaction.Status = ex is ArgumentException or InvalidOperationException ? "Failed" : "Pending";
            transaction.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(CancellationToken.None);
            _logger.LogError(ex, "PhonePe checkout could not be created for merchant order {MerchantOrderId}.", merchantOrderId);
            var message = transaction.Status == "Failed"
                ? "The payment could not be started. Please check the payment configuration and try again."
                : "We could not confirm the payment gateway response. Your payment order remains pending and course access was not granted.";
            if (wantsJsonResponse) return StatusCode(StatusCodes.Status502BadGateway, new { message });
            TempData["CoursePaymentMessage"] = message;
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    [HttpGet("Courses/PhonePeReturn")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> PhonePeReturn(
        string merchantOrderId,
        CancellationToken cancellationToken,
        string? returnUrl = null)
    {
        var wantsJsonResponse = Request.Headers.Accept.Any(value => value.Contains("application/json", StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(merchantOrderId))
        {
            if (wantsJsonResponse) return BadRequest(new { message = "Payment order ID is required." });
            return RedirectToAction(nameof(CourseHome));
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();

        var transaction = await _dbContext.PaymentTransactions.FirstOrDefaultAsync(
            item => item.MerchantOrderId == merchantOrderId && item.UserId == userId,
            cancellationToken);

        if (transaction is null)
        {
            if (wantsJsonResponse) return NotFound(new { message = "The payment order could not be found." });
            TempData["CoursePaymentMessage"] = "The payment order could not be found.";
            return RedirectToAction(nameof(CourseHome));
        }

        string? state = null;
        string message;

        try
        {
            state = await _phonePePaymentService.GetOrderStateAsync(merchantOrderId, cancellationToken);
            await ProcessPhonePeStateAsync(transaction, state, cancellationToken);

            if (string.Equals(transaction.PaymentPurpose, "ExamRetake", StringComparison.Ordinal))
            {
                message = string.Equals(state, "COMPLETED", StringComparison.OrdinalIgnoreCase)
                    ? "Payment successful. Your paid re-exam is unlocked; open Examinations to start it."
                    : string.Equals(state, "FAILED", StringComparison.OrdinalIgnoreCase)
                        ? "Re-exam payment failed. No attempt was unlocked."
                        : "Re-exam payment is still being processed. No attempt has been unlocked yet.";
            }
            else if (string.Equals(state, "COMPLETED", StringComparison.OrdinalIgnoreCase))
            {
                message = "Payment successful. Your course is now available.";
            }
            else if (string.Equals(state, "FAILED", StringComparison.OrdinalIgnoreCase))
            {
                message = "Payment failed. No course access was granted.";
            }
            else
            {
                message = "Payment is still being processed. No course access has been granted yet.";
            }
        }
        catch (TimeoutException ex)
        {
            _logger.LogWarning(ex, "PhonePe status verification timed out for merchant order {MerchantOrderId}.", merchantOrderId);
            message = "We could not confirm the payment because the payment gateway timed out. Your order remains pending.";
            if (wantsJsonResponse) return StatusCode(StatusCodes.Status504GatewayTimeout, new { success = false, state = "PENDING", message, courseId = transaction.CourseId });

            if (string.Equals(transaction.PaymentPurpose, "ExamRetake", StringComparison.Ordinal))
            {
                TempData["ExamError"] = message;
                if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    var separator = returnUrl.Contains('?') ? "&" : "?";
                    return Redirect($"{returnUrl}{separator}reopenExamModal=true");
                }

                return RedirectToAction("Index", "StudentExaminations");
            }

            TempData["CoursePaymentMessage"] = message;
            return RedirectToAction(nameof(Details), new { id = transaction.CourseId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PhonePe status verification failed for merchant order {MerchantOrderId}.", merchantOrderId);
            message = "We could not verify the payment yet. Your order remains pending. Please try again shortly.";
            if (wantsJsonResponse) return StatusCode(StatusCodes.Status503ServiceUnavailable, new { success = false, state = "PENDING", message, courseId = transaction.CourseId });

            if (string.Equals(transaction.PaymentPurpose, "ExamRetake", StringComparison.Ordinal))
            {
                TempData["ExamError"] = message;
                if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    var separator = returnUrl.Contains('?') ? "&" : "?";
                    return Redirect($"{returnUrl}{separator}reopenExamModal=true");
                }

                return RedirectToAction("Index", "StudentExaminations");
            }

            TempData["CoursePaymentMessage"] = message;
            return RedirectToAction(nameof(Details), new { id = transaction.CourseId });
        }

        var completed = string.Equals(state, "COMPLETED", StringComparison.OrdinalIgnoreCase);

        if (wantsJsonResponse)
        {
            return Json(new
            {
                success = completed,
                state,
                message,
                courseId = transaction.CourseId
            });
        }

        if (string.Equals(transaction.PaymentPurpose, "ExamRetake", StringComparison.Ordinal))
        {
            TempData[completed ? "ExamMessage" : "ExamError"] = message;

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                var separator = returnUrl.Contains('?') ? "&" : "?";
                return Redirect($"{returnUrl}{separator}reopenExamModal=true");
            }

            return RedirectToAction("Index", "StudentExaminations");
        }

        TempData["CoursePaymentMessage"] = message;

        if (completed)
            return RedirectToAction(nameof(CourseEnvironment), new { id = transaction.CourseId });

        return RedirectToAction(nameof(Details), new { id = transaction.CourseId });
    }

    [HttpPost("Courses/PhonePeCallback")]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> PhonePeCallback(CancellationToken cancellationToken)
    {
        var authorization = Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authorization)) return Unauthorized();
        using var reader = new StreamReader(Request.Body);
        var requestBody = await reader.ReadToEndAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(requestBody)) return BadRequest();

        try
        {
            var callback = _phonePePaymentService.ValidateCallback(authorization, requestBody);
            if (callback.Payload is null) return BadRequest();

            var merchantOrderId = callback.Payload.OrderId;
            var state = callback.Payload.State;
            if (string.IsNullOrWhiteSpace(merchantOrderId) || string.IsNullOrWhiteSpace(state)) return BadRequest();

            var transaction = await _dbContext.PaymentTransactions.FirstOrDefaultAsync(
                item => item.MerchantOrderId == merchantOrderId,
                cancellationToken);

            if (transaction is null) return NotFound();

            await ProcessPhonePeStateAsync(transaction, state, cancellationToken);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Invalid PhonePe callback received.");
            return Unauthorized();
        }
    }

    private async Task ProcessPhonePeStateAsync(PaymentTransaction transaction, string state, CancellationToken cancellationToken)
    {
        if (string.Equals(transaction.PaymentPurpose, "ExamRetake", StringComparison.Ordinal))
        {
            if (string.Equals(state, "COMPLETED", StringComparison.OrdinalIgnoreCase))
            {
                transaction.Status = "Completed";
                transaction.CompletedAt ??= DateTime.UtcNow;
            }
            else
            {
                transaction.Status = string.Equals(state, "FAILED", StringComparison.OrdinalIgnoreCase)
                    ? "Failed"
                    : "Pending";
            }

            transaction.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        if (string.Equals(state, "COMPLETED", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(transaction.Status, "Completed", StringComparison.OrdinalIgnoreCase)) return;

            var alreadyPurchased = await _coursePurchaseService.HasPurchasedCourseAsync(
                transaction.UserId,
                transaction.CourseId,
                cancellationToken);

            var purchaseCompleted = await _coursePurchaseService.CompleteWithPriceAsync(
                transaction.UserId,
                transaction.CourseId,
                transaction.Amount,
                cancellationToken);

            if (!purchaseCompleted)
            {
                transaction.Status = "Failed";
                transaction.UpdatedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync(cancellationToken);
                return;
            }

            transaction.Status = "Completed";
            transaction.CompletedAt = DateTime.UtcNow;
            transaction.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            if (!alreadyPurchased)
            {
                await TrySendPurchaseInvoiceAsync(transaction.UserId, transaction.CourseId, cancellationToken);
            }

            return;
        }

        var wasAlreadyFailed = string.Equals(transaction.Status, "Failed", StringComparison.OrdinalIgnoreCase);
        transaction.Status = string.Equals(state, "FAILED", StringComparison.OrdinalIgnoreCase)
            ? "Failed"
            : "Pending";
        transaction.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (string.Equals(state, "FAILED", StringComparison.OrdinalIgnoreCase) && !wasAlreadyFailed)
        {
            await TrySendPaymentFailureEmailAsync(transaction, cancellationToken);
        }
    }

    private async Task TrySendPurchaseInvoiceAsync(string userId, int courseId, CancellationToken cancellationToken)
    {
        var purchase = await _dbContext.StudentCoursePurchases
            .AsNoTracking()
            .Where(item => item.UserId == userId && item.CourseId == courseId)
            .OrderByDescending(item => item.PurchasedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var course = await _dbContext.Courses
            .AsNoTracking()
            .Where(item => item.Id == courseId)
            .Select(item => new { item.Title })
            .FirstOrDefaultAsync(cancellationToken);

        var user = await _dbContext.Users
            .AsNoTracking()
            .Where(item => item.Id == userId && item.IsActive)
            .Select(item => new { item.Email, item.UserName })
            .FirstOrDefaultAsync(cancellationToken);

        if (purchase is null || course is null || string.IsNullOrWhiteSpace(user?.Email)) return;

        try
        {
            await _emailService.SendPurchaseInvoiceEmailAsync(
                user.Email,
                user.UserName ?? "Student",
                course.Title,
                purchase.PurchasePrice,
                purchase.Id,
                purchase.PurchasedAt,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Purchase invoice could not be sent for purchase {PurchaseId} to {Email}.",
                purchase.Id,
                user.Email);
        }
    }

    private async Task TrySendPaymentFailureEmailAsync(PaymentTransaction transaction, CancellationToken cancellationToken)
    {
        var courseTitle = await _dbContext.Courses
            .AsNoTracking()
            .Where(item => item.Id == transaction.CourseId)
            .Select(item => item.Title)
            .FirstOrDefaultAsync(cancellationToken);

        var user = await _dbContext.Users
            .AsNoTracking()
            .Where(item => item.Id == transaction.UserId && item.IsActive)
            .Select(item => new { item.Email, item.UserName })
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(user?.Email) || string.IsNullOrWhiteSpace(courseTitle)) return;

        try
        {
            await _paymentNotificationService.SendPaymentFailureEmailAsync(
                user.Email,
                user.UserName ?? "Student",
                courseTitle,
                transaction.Amount,
                transaction.MerchantOrderId,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Payment failure email could not be sent for merchant order {MerchantOrderId} to {Email}.",
                transaction.MerchantOrderId,
                user.Email);
        }
    }

    [HttpGet("Courses/Home")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> CourseHome(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        var activeCourseIds = await _coursePurchaseService.GetAccessibleCourseIdsAsync(
            userId,
            cancellationToken);
        var publishedCourses = await _courseService.GetPublishedAsync(cancellationToken);
        var activeCourseIdSet = activeCourseIds.ToHashSet();

        var courses = publishedCourses
            .Where(course => activeCourseIdSet.Contains(course.Id))
            .OrderBy(course => course.Title)
            .ToList();

        return View("MyCourses", courses);
    }

    [HttpGet("Courses/PaymentHistory")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> PaymentHistory(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        var purchases = await _dbContext.StudentCoursePurchases
            .AsNoTracking()
            .Where(purchase => purchase.UserId == userId)
            .Join(
                _dbContext.Courses.AsNoTracking(),
                purchase => purchase.CourseId,
                course => course.Id,
                (purchase, course) => new CoursePurchaseHistoryViewModel
                {
                    PurchaseId = purchase.Id,
                    CourseTitle = course.Title,
                    Amount = purchase.PurchasePrice,
                    PurchasedAt = purchase.PurchasedAt
                })
            .OrderByDescending(purchase => purchase.PurchasedAt)
            .ToListAsync(cancellationToken);

        return View(purchases);
    }

    [HttpGet("Courses/Course/{id:int}")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> CourseEnvironment(
        int id,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            return NotFound();
        }

        CourseLearningDto? course;

        try
        {
            course = await _courseService.GetLearningAsync(id, null, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Ignore the abandoned request when the user navigates to another page.
            return new EmptyResult();
        }

        if (course is null)
        {
            return NotFound();
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        var hasAccess = TempData.Peek($"DemoPayment:{id}") is not null
            || await _coursePurchaseService.CanAccessCourseAsync(userId, id, cancellationToken);

        if (!hasAccess)
        {
            TempData["CoursePaymentMessage"] =
                "Payment is required before this paid course can be opened.";
            return RedirectToAction(nameof(Details), new { id });
        }

        course.Progress = await _studentProgressService.GetProgressAsync(
            userId,
            id,
            cancellationToken);

        return View("CourseHome", course);
    }

    [HttpGet("Courses/Curriculum/{id:int}")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> Curriculum(
        int id,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            return NotFound();
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        var hasAccess = TempData.Peek($"DemoPayment:{id}") is not null
            || await _coursePurchaseService.CanAccessCourseAsync(
                userId,
                id,
                cancellationToken);

        if (!hasAccess)
        {
            TempData["CoursePaymentMessage"] =
                "Payment is required before this paid course can be opened.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var course = await _courseService.GetLearningAsync(id, null, cancellationToken);

        if (course is null)
        {
            return NotFound();
        }

        course.Progress = await _studentProgressService.GetProgressAsync(
            userId,
            id,
            cancellationToken);

        return View(course);
    }

    [HttpGet("Courses/Learn/{id:int}")]
    public async Task<IActionResult> Learn(
        int id,
        int? contentId,
        int? topicId,
        int? chapterId,
        CancellationToken cancellationToken,
        bool reopenExamModal = false)
    {
        if (id <= 0
            || (contentId.HasValue && contentId.Value <= 0)
            || (topicId.HasValue && topicId.Value <= 0)
            || (chapterId.HasValue && chapterId.Value <= 0))
        {
            return NotFound();
        }

        var publishedCourseExists = await _dbContext.Courses
            .AsNoTracking()
            .AnyAsync(course => course.Id == id && course.Flag == 0, cancellationToken);

        if (!publishedCourseExists)
        {
            return NotFound();
        }

        var studentUserId = User.IsInRole("Student")
            ? User.FindFirstValue(ClaimTypes.NameIdentifier)
            : null;
        var accessUserId = User.Identity?.IsAuthenticated == true
            ? User.FindFirstValue(ClaimTypes.NameIdentifier)
            : null;

        var demoPaymentCompleted = User.IsInRole("Student")
            && TempData.Peek($"DemoPayment:{id}") is not null;

        var hasCourseAccess = demoPaymentCompleted
            || (User.IsInRole("Student")
                ? await _coursePurchaseService.CanAccessCourseAsync(
                    accessUserId,
                    id,
                    cancellationToken)
                : await _coursePurchaseService.GetCoursePriceAsync(
                    id,
                    cancellationToken) <= 0m);

        if (!hasCourseAccess)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                var returnUrl = Url.Action(nameof(Details), new { id });
                return RedirectToAction("Login", "Account", new { returnUrl });
            }

            TempData["CoursePaymentMessage"] =
                "Payment is required before this paid course can be opened.";

            return RedirectToAction(nameof(Details), new { id });
        }

        if (chapterId.HasValue)
        {
            var chapterTopicId = await (
                from note in _dbContext.TrainingNotes.AsNoTracking()
                join courseModule in _dbContext.CourseModules.AsNoTracking()
                    on note.TopicId equals courseModule.TopicId
                where note.Id == chapterId.Value
                    && note.Flag == 0
                    && courseModule.CourseId == id
                    && courseModule.Flag == 0
                select (int?)note.TopicId)
                .FirstOrDefaultAsync(cancellationToken);

            if (!chapterTopicId.HasValue
                || (topicId.HasValue && topicId.Value != chapterTopicId.Value))
            {
                return NotFound();
            }

            topicId = chapterTopicId.Value;
            contentId = await _dbContext.Lessons
                .AsNoTracking()
                .Where(lesson => lesson.NoteId == chapterId.Value && lesson.Flag == 0)
                .OrderBy(lesson => lesson.Id)
                .Select(lesson => (int?)lesson.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (!chapterId.HasValue
            && !string.IsNullOrWhiteSpace(studentUserId)
            && !contentId.HasValue
            && !topicId.HasValue)
        {
            var resumeContentId = await _studentProgressService.GetResumeContentIdAsync(
                studentUserId,
                id,
                cancellationToken);

            if (resumeContentId.HasValue)
            {
                contentId = resumeContentId.Value;
            }
        }

        // Resolve the selected topic's lesson before loading its full learning DTO.
        // This prevents loading every chapter, note and assessment twice on topic changes.
        if (topicId.HasValue && !contentId.HasValue && !chapterId.HasValue)
        {
            var topicExistsInCourse = await (
                from courseModule in _dbContext.CourseModules.AsNoTracking()
                join topic in _dbContext.Topics.AsNoTracking()
                    on courseModule.TopicId equals topic.Id
                where courseModule.CourseId == id
                    && courseModule.TopicId == topicId.Value
                    && courseModule.Flag == 0
                    && topic.Flag == 0
                select topic.Id)
                .AnyAsync(cancellationToken);

            if (!topicExistsInCourse)
            {
                return NotFound();
            }

            var directTopicContentIds = await _dbContext.Lessons
                .AsNoTracking()
                .Where(lesson => lesson.TopicId == topicId.Value && lesson.Flag == 0)
                .OrderBy(lesson => lesson.Id)
                .Select(lesson => lesson.Id)
                .ToListAsync(cancellationToken);

            var chapterContentIds = await (
                from lesson in _dbContext.Lessons.AsNoTracking()
                join note in _dbContext.TrainingNotes.AsNoTracking()
                    on lesson.NoteId equals (int?)note.Id
                where lesson.Flag == 0
                    && note.TopicId == topicId.Value
                    && note.Flag == 0
                orderby note.SortOrder, note.Id, lesson.Id
                select lesson.Id)
                .Distinct()
                .ToListAsync(cancellationToken);

            var topicContentIds = directTopicContentIds
                .Concat(chapterContentIds)
                .Distinct()
                .ToList();

            if (topicContentIds.Count > 0)
            {
                contentId = topicContentIds[0];

                if (!string.IsNullOrWhiteSpace(studentUserId))
                {
                    var resumeContentId = await _studentProgressService.GetResumeContentIdAsync(
                        studentUserId,
                        id,
                        cancellationToken);

                    if (resumeContentId.HasValue && topicContentIds.Contains(resumeContentId.Value))
                    {
                        contentId = resumeContentId.Value;
                    }
                }
            }
        }

        var course = await _courseService.GetLearningAsync(id, contentId, cancellationToken, topicId, chapterId);

        if (course is null)
        {
            return NotFound();
        }

        if (!string.IsNullOrWhiteSpace(studentUserId))
        {
            var allowedTopicId = await _topicExaminationService.GetCurrentTopicIdAsync(
                studentUserId, id, cancellationToken);
            ViewBag.AllowedTopicId = allowedTopicId;
            var requestedTopicId = topicId ?? course.CurrentTopicId;
            if (requestedTopicId is null && contentId.HasValue)
            {
                requestedTopicId = course.Topics.FirstOrDefault(topic =>
                    topic.Contents.Any(content => content.Id == contentId.Value)
                    || topic.Chapters.Any(chapter => chapter.Contents.Any(content => content.Id == contentId.Value)))?.Id;
            }

            if (allowedTopicId.HasValue && requestedTopicId.HasValue)
            {
                if (requestedTopicId.Value != allowedTopicId.Value)
                {
                    TempData["LearningProgressMessage"] = "This topic is locked until you pass the current topic examination.";
                    return RedirectToAction(nameof(Learn), new { id, topicId = allowedTopicId.Value });
                }
            }
        }

        if (topicId.HasValue)
        {
            var selectedTopic = course.Topics.FirstOrDefault(topic => topic.Id == topicId.Value);
            if (selectedTopic is null)
            {
                return NotFound();
            }

            course.CurrentTopicId = selectedTopic.Id;
        }

        if (chapterId.HasValue)
        {
            course.CurrentChapterId = chapterId.Value;
        }

        if (!string.IsNullOrWhiteSpace(studentUserId))
        {
            if (course.CurrentContentId.HasValue)
            {
                try
                {
                    await _studentProgressService.RecordAccessAsync(
                        studentUserId,
                        id,
                        course.CurrentContentId.Value,
                        cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // A newer navigation can cancel this request while access progress is being saved.
                    // The abandoned request no longer needs to finish rendering a response.
                    return new EmptyResult();
                }
            }

            course.Progress = await _studentProgressService.GetProgressAsync(
                studentUserId,
                id,
                cancellationToken);

            // Recompute completion from persisted lesson progress via each lesson's NoteId.
            var chapterIds = course.Topics
                .SelectMany(topic => topic.Chapters)
                .Select(chapter => chapter.Id)
                .Distinct()
                .ToList();

            if (chapterIds.Count > 0)
            {
                var lessonIdsByChapter = await _dbContext.Lessons
                    .AsNoTracking()
                    .Where(lesson => lesson.Flag == 0
                        && lesson.NoteId.HasValue
                        && chapterIds.Contains(lesson.NoteId.Value))
                    .GroupBy(lesson => lesson.NoteId!.Value)
                    .Select(group => new
                    {
                        ChapterId = group.Key,
                        ContentIds = group.Select(lesson => lesson.Id).Distinct().ToList()
                    })
                    .ToListAsync(cancellationToken);

                var completedContentIds = course.Progress.CompletedContentIds.ToHashSet();
                var completedChapterIds = lessonIdsByChapter
                    .Where(group => group.ContentIds.Count > 0
                        && group.ContentIds.All(completedContentIds.Contains))
                    .Select(group => group.ChapterId)
                    .ToHashSet();

                foreach (var chapter in course.Topics.SelectMany(topic => topic.Chapters))
                {
                    chapter.IsCompleted = completedChapterIds.Contains(chapter.Id);
                }
            }

            var examTopicId = course.CurrentTopicId ?? topicId;
            if (examTopicId.HasValue)
            {
                var examTopic = course.Topics.FirstOrDefault(t => t.Id == examTopicId.Value);
                var topicContentIds = examTopic is null
                    ? new List<int>()
                    : examTopic.Contents.Concat(examTopic.Chapters.SelectMany(chapter => chapter.Contents))
                        .Select(content => content.Id).Distinct().ToList();
                var topicCompleted = topicContentIds.Count == 0
                    || topicContentIds.All(course.Progress.CompletedContentIds.Contains);
                var topicAttempts = await _dbContext.TopicExamAttempts.AsNoTracking()
                    .Where(attempt => attempt.UserId == studentUserId
                        && attempt.CourseId == id && attempt.TopicId == examTopicId.Value)
                    .OrderByDescending(attempt => attempt.Id)
                    .Select(attempt => new
                    {
                        attempt.Status,
                        attempt.Score,
                        attempt.TotalQuestions,
                        attempt.Passed
                    })
                    .ToListAsync(cancellationToken);
                var hasActiveExam = topicAttempts.Any(attempt =>
                    attempt.Status is "Scheduled" or "Ready" or "InProgress");
                var hasEverPassed = topicAttempts.Any(attempt =>
                    attempt.Passed && attempt.Status == "Passed");
                var nextAttemptNumber = topicAttempts.Count + 1;
                var requiresRetakePayment = nextAttemptNumber > 2 || hasEverPassed;
                var retakeBaseFee = Math.Max(1, nextAttemptNumber - 2) * 100m;
                var retakeFee = retakeBaseFee
                    + Math.Round(retakeBaseFee * 0.18m, 2, MidpointRounding.AwayFromZero);

                var pendingRetakePayment = await _dbContext.PaymentTransactions
                    .Where(payment => payment.UserId == studentUserId
                        && payment.CourseId == id
                        && payment.PaymentPurpose == "ExamRetake"
                        && payment.ExamTopicId == examTopicId.Value
                        && payment.ExamAttemptNumber == nextAttemptNumber
                        && (payment.Status == "Created" || payment.Status == "Pending"))
                    .OrderByDescending(payment => payment.CreatedAt)
                    .FirstOrDefaultAsync(cancellationToken);

                // Do not block the student in a pending-payment verification state.
                // The checkout exit callback records cancellation/decline immediately.
                // Recover older abandoned orders from the previous flow after five minutes.
                if (pendingRetakePayment is not null
                    && pendingRetakePayment.CreatedAt <= DateTime.UtcNow.AddMinutes(-5))
                {
                    pendingRetakePayment.Status = "Failed";
                    pendingRetakePayment.UpdatedAt = DateTime.UtcNow;
                    await _dbContext.SaveChangesAsync(cancellationToken);
                }

                var hasUnusedPaidRetake = await _dbContext.PaymentTransactions.AsNoTracking().AnyAsync(payment =>
                    payment.UserId == studentUserId
                    && payment.CourseId == id
                    && payment.PaymentPurpose == "ExamRetake"
                    && payment.ExamTopicId == examTopicId.Value
                    && payment.ExamAttemptNumber == nextAttemptNumber
                    && payment.Status == "Completed"
                    && payment.ExamAttemptId == null,
                    cancellationToken);

                var pendingRetakePaymentOrderId = await _dbContext.PaymentTransactions.AsNoTracking()
                    .Where(payment => payment.UserId == studentUserId
                        && payment.CourseId == id
                        && payment.PaymentPurpose == "ExamRetake"
                        && payment.ExamTopicId == examTopicId.Value
                        && payment.ExamAttemptNumber == nextAttemptNumber
                        && (payment.Status == "Created" || payment.Status == "Pending"))
                    .OrderByDescending(payment => payment.CreatedAt)
                    .Select(payment => payment.MerchantOrderId)
                    .FirstOrDefaultAsync(cancellationToken);
                var hasPendingRetakePayment = !string.IsNullOrWhiteSpace(pendingRetakePaymentOrderId);

                ViewBag.PendingRetakePaymentOrderId = pendingRetakePaymentOrderId;
                ViewBag.ShowExamApplicationModal = topicCompleted
                    && (reopenExamModal || TempData["OpenExamApplicationModal"] is true);
                ViewBag.ExamHasActiveAttempt = hasActiveExam;
                ViewBag.ExamApplicationTopicId = examTopicId.Value;
                ViewBag.ExamAttemptNumber = nextAttemptNumber;
                ViewBag.ExamRequiresPayment = requiresRetakePayment;
                ViewBag.ExamRetakeFee = retakeFee;
                ViewBag.ExamHasEverPassed = hasEverPassed;
                ViewBag.ExamHasUnusedPaidRetake = hasUnusedPaidRetake;
                ViewBag.ExamHasPendingRetakePayment = hasPendingRetakePayment;
                ViewBag.PreviousExamStatus = topicAttempts.FirstOrDefault()?.Status;
                ViewBag.PreviousExamScore = topicAttempts.FirstOrDefault()?.Score;
                ViewBag.PreviousExamTotalQuestions = topicAttempts.FirstOrDefault()?.TotalQuestions;
            }
        }

        return View(course);
    }

    [HttpGet("/uploads/videos/{playbackId}")]
    public async Task<IActionResult> MuxVideo(
        string playbackId,
        int? courseId,
        string? title,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(playbackId)
            || playbackId.Length > 256
            || playbackId.Contains('/')
            || playbackId.Contains('\\')
            || playbackId.Contains('.')
            || playbackId.Any(char.IsWhiteSpace))
        {
            return NotFound();
        }

        if (courseId is > 0)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!User.IsInRole("Student") || string.IsNullOrWhiteSpace(userId))
            {
                return Challenge();
            }

            var hasAccess = TempData.Peek($"DemoPayment:{courseId.Value}") is not null
                || await _coursePurchaseService.CanAccessCourseAsync(
                    userId,
                    courseId.Value,
                    cancellationToken);

            if (!hasAccess)
            {
                TempData["CoursePaymentMessage"] =
                    "Payment is required before this paid course can be opened.";
                return RedirectToAction(nameof(Details), new { id = courseId.Value });
            }

            var course = await _courseService.GetByIdAsync(courseId.Value, cancellationToken);

            if (course is null)
            {
                return NotFound();
            }

            try
            {
                var publicPlaybackId = await _muxVideoService.EnsurePublicPlaybackIdAsync(
                    playbackId,
                    cancellationToken);

                ViewData["CourseId"] = course.Id.ToString();
                ViewData["CourseTitle"] = course.Title;
                ViewData["VideoTitle"] = string.IsNullOrWhiteSpace(title)
                    ? "Video lesson"
                    : title.Trim();
                ViewData["MuxPlaybackId"] = publicPlaybackId;

                return View("Watch");
            }
            catch (InvalidOperationException)
            {
                return NotFound();
            }
        }

        try
        {
            var publicPlaybackId = await _muxVideoService.EnsurePublicPlaybackIdAsync(
                playbackId,
                cancellationToken);

            return Redirect($"https://player.mux.com/{Uri.EscapeDataString(publicPlaybackId)}");
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }

    [HttpPost("Courses/Learn/{id:int}/Complete/{contentId:int}")]
    [Authorize(Roles = "Student")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CompleteLesson(
        int id,
        int contentId,
        CancellationToken cancellationToken)
    {
        if (id <= 0 || contentId <= 0)
        {
            return NotFound();
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var hasDemoAccess = TempData.Peek($"DemoPayment:{id}") is not null;
        var hasCourseAccess = hasDemoAccess
            || await _coursePurchaseService.CanAccessCourseAsync(
                userId,
                id,
                cancellationToken);

        if (!hasCourseAccess)
        {
            TempData["CoursePaymentMessage"] =
                "Payment is required before paid-course progress can be updated.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var allowedTopicId = await _topicExaminationService.GetCurrentTopicIdAsync(userId, id, cancellationToken);
        var contentTopicId = await (
            from lesson in _dbContext.Lessons.AsNoTracking()
            join topic in _dbContext.Topics.AsNoTracking() on lesson.TopicId equals topic.Id into topicJoin
            from topic in topicJoin.DefaultIfEmpty()
            join note in _dbContext.TrainingNotes.AsNoTracking() on lesson.NoteId equals note.Id into noteJoin
            from note in noteJoin.DefaultIfEmpty()
            where lesson.Id == contentId && lesson.Flag == 0
            select (int?)(topic != null ? topic.Id : note != null ? note.TopicId : null))
            .SingleOrDefaultAsync(cancellationToken);

        if (allowedTopicId.HasValue && contentTopicId.HasValue && allowedTopicId.Value != contentTopicId.Value)
        {
            return Forbid();
        }

        var markedCompleted = await _studentProgressService.MarkCompletedAsync(
            userId,
            id,
            contentId,
            cancellationToken);

        if (!markedCompleted)
        {
            return NotFound();
        }

        TempData["LearningProgressMessage"] = "Lesson marked as complete.";

        return RedirectToAction(nameof(Learn), new { id, contentId });
    }

    [HttpPost("Courses/Learn/{id:int}/CompleteChapter/{topicId:int}/{chapterId:int}", Name = "CompleteCourseChapter")]
    [Authorize(Roles = "Student")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CompleteChapter(
        int id,
        int topicId,
        int chapterId,
        CancellationToken cancellationToken)
    {
        if (id <= 0 || topicId <= 0 || chapterId <= 0)
            return NotFound();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var hasDemoAccess = TempData.Peek($"DemoPayment:{id}") is not null;
        var hasCourseAccess = hasDemoAccess
            || await _coursePurchaseService.CanAccessCourseAsync(userId, id, cancellationToken);
        if (!hasCourseAccess)
        {
            TempData["CoursePaymentMessage"] = "Payment is required before chapter progress can be updated.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // Use the same chapter sequence as the learning page so the completion
        // endpoint and the UI cannot disagree about which chapter comes next.
        var learningCourse = await _courseService.GetLearningAsync(
            id, null, cancellationToken, topicId, chapterId);
        var learningTopic = learningCourse?.Topics.FirstOrDefault(topic => topic.Id == topicId);
        if (learningTopic is null)
            return NotFound();

        var orderedChapters = learningTopic.Chapters
            .OrderBy(chapter => chapter.SortOrder)
            .ThenBy(chapter => chapter.Id)
            .ToList();
        var currentIndex = orderedChapters.FindIndex(chapter => chapter.Id == chapterId);
        if (currentIndex < 0)
            return NotFound();

        if (currentIndex >= orderedChapters.Count - 1)
        {
            TempData["LearningProgressMessage"] =
                "This is the final chapter. Use Apply for Examination to continue.";
            return RedirectToAction(nameof(Learn), new { id, topicId, chapterId });
        }

        var allowedTopicId = await _topicExaminationService.GetCurrentTopicIdAsync(
            userId, id, cancellationToken);
        if (allowedTopicId.HasValue && allowedTopicId.Value != topicId)
        {
            TempData["LearningProgressMessage"] =
                "Complete the current topic examination before continuing to another topic.";
            return RedirectToAction(nameof(Learn), new { id, topicId = allowedTopicId.Value });
        }

        var contentIds = orderedChapters[currentIndex].Contents
            .Select(content => content.Id)
            .Distinct()
            .ToList();

        // Include lesson rows linked directly to the chapter, while retaining
        // DTO content IDs so existing chapter navigation keeps working.
        var linkedContentIds = await _dbContext.Lessons
            .AsNoTracking()
            .Where(lesson => lesson.Flag == 0 && lesson.NoteId == chapterId)
            .Select(lesson => lesson.Id)
            .ToListAsync(cancellationToken);

        contentIds = contentIds
            .Concat(linkedContentIds)
            .Distinct()
            .ToList();
        foreach (var contentId in contentIds)
        {
            var marked = await _studentProgressService.MarkCompletedAsync(
                userId, id, contentId, cancellationToken);
            if (!marked)
            {
                TempData["LearningProgressMessage"] =
                    "Chapter completion could not be saved. Please refresh and try again.";
                return RedirectToAction(nameof(Learn), new { id, topicId, chapterId });
            }
        }

        var nextChapterId = orderedChapters[currentIndex + 1].Id;
        TempData["LearningProgressMessage"] = "Chapter completed. Continue with the next chapter.";
        return RedirectToAction(nameof(Learn), new { id, topicId, chapterId = nextChapterId });
    }

    [HttpPost("Courses/Learn/{id:int}/CompleteTopic/{topicId:int}")]
    [Authorize(Roles = "Student")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CompleteTopic(
        int id,
        int topicId,
        CancellationToken cancellationToken)
    {
        if (id <= 0 || topicId <= 0)
            return NotFound();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var hasDemoAccess = TempData.Peek($"DemoPayment:{id}") is not null;
        var hasCourseAccess = hasDemoAccess
            || await _coursePurchaseService.CanAccessCourseAsync(userId, id, cancellationToken);
        if (!hasCourseAccess)
        {
            TempData["CoursePaymentMessage"] = "Payment is required before paid-course progress can be updated.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var topicBelongsToCourse = await _dbContext.CourseModules.AsNoTracking()
            .AnyAsync(x => x.CourseId == id && x.TopicId == topicId && x.Flag == 0, cancellationToken);
        if (!topicBelongsToCourse)
            return NotFound();

        var questionAvailability = await _topicExaminationService.CheckQuestionAvailabilityAsync(
            userId,
            id,
            topicId,
            cancellationToken);
        if (!questionAvailability.IsAvailable)
        {
            var examAvailabilityMessage = questionAvailability.Error
                ?? "Exam questions are not available for this topic right now. Please try applying again later.";
            TempData["ExamError"] = examAvailabilityMessage;

            await NotifyExamQuestionsUnavailableAsync(
                userId,
                id,
                topicId,
                questionAvailability.ValidQuestionCount,
                examAvailabilityMessage,
                cancellationToken);

            return RedirectToAction(nameof(Learn), new { id, topicId });
        }

        var hasExistingExam = await _dbContext.TopicExamAttempts.AsNoTracking().AnyAsync(
            attempt => attempt.UserId == userId
                && attempt.CourseId == id
                && attempt.TopicId == topicId
                && (attempt.Status == "Scheduled"
                    || attempt.Status == "Ready"
                    || attempt.Status == "InProgress"
                    || attempt.Status == "Passed"),
            cancellationToken);
        if (hasExistingExam)
        {
            TempData["ExamMessage"] =
                "An examination record already exists for this topic. Open it to continue or review your result.";
            return RedirectToAction("Index", "StudentExaminations");
        }

        var allowedTopicId = await _topicExaminationService.GetCurrentTopicIdAsync(
            userId,
            id,
            cancellationToken);
        if (allowedTopicId.HasValue && allowedTopicId.Value != topicId)
        {
            TempData["LearningProgressMessage"] =
                "Complete the current topic examination before applying for a different topic.";
            return RedirectToAction(nameof(Learn), new { id, topicId = allowedTopicId.Value });
        }

        var topicNoteIds = await _dbContext.TrainingNotes.AsNoTracking()
            .Where(x => x.TopicId == topicId && x.Flag == 0)
            .Select(x => x.Id).ToListAsync(cancellationToken);
        var contentIds = await _dbContext.Lessons.AsNoTracking()
            .Where(x => x.Flag == 0 && (x.TopicId == topicId
                || (x.NoteId.HasValue && topicNoteIds.Contains(x.NoteId.Value))))
            .Select(x => x.Id).Distinct().ToListAsync(cancellationToken);
        foreach (var contentId in contentIds)
        {
            var marked = await _studentProgressService.MarkCompletedAsync(userId, id, contentId, cancellationToken);
            if (!marked)
            {
                TempData["LearningProgressMessage"] = "Topic completion could not be saved. Please refresh and try again.";
                return RedirectToAction(nameof(Learn), new { id, topicId });
            }
        }

        TempData["LearningProgressMessage"] = "Topic marked as complete. You can now apply for its examination.";
        TempData["OpenExamApplicationModal"] = true;
        return RedirectToAction(nameof(Learn), new { id, topicId });
    }

    private Task NotifyExamQuestionsUnavailableAsync(
        string userId,
        int courseId,
        int topicId,
        int validQuestionCount,
        string reason,
        CancellationToken cancellationToken)
    {
        return _examApplicationNotificationService.CreateMissingQuestionsNotificationsAsync(
            userId,
            User.Identity?.Name ?? userId,
            courseId,
            topicId,
            validQuestionCount,
            reason,
            cancellationToken);
    }

    [HttpPost("Courses/Learn/{id:int}/Assessment/{contentId:int}")]
    [HttpPost("Courses/Learn/{id:int}/TopicAssessment/{topicId:int}")]
    [Authorize(Roles = "Student")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitAssessment(
        int id,
        int? contentId,
        int? topicId,
        [FromForm] List<AssessmentAnswerDto> answers,
        CancellationToken cancellationToken)
    {
        if (id <= 0
            || (contentId is null && topicId is null)
            || contentId is <= 0
            || topicId is <= 0)
        {
            return NotFound();
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var hasDemoAccess = TempData.Peek($"DemoPayment:{id}") is not null;
        var hasCourseAccess = hasDemoAccess
            || await _coursePurchaseService.CanAccessCourseAsync(
                userId,
                id,
                cancellationToken);

        if (!hasCourseAccess)
        {
            TempData["CoursePaymentMessage"] =
                "Payment is required before a paid-course assessment can be submitted.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var outcome = await _courseAssessmentService.SubmitAsync(
            userId,
            id,
            contentId,
            topicId,
            answers ?? new List<AssessmentAnswerDto>(),
            cancellationToken);

        if (!outcome.Succeeded || outcome.Result is null)
        {
            TempData["AssessmentError"] = outcome.ErrorMessage
                ?? "The assessment could not be submitted. Please try again.";

            return RedirectToAction(nameof(Learn), new { id, contentId, topicId });
        }

        return RedirectToAction(
            nameof(AssessmentResult),
            new { id, attemptId = outcome.Result.Id });
    }

    [HttpGet("Courses/Learn/{id:int}/AssessmentResult/{attemptId:int}")]
    [Authorize(Roles = "Student")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> AssessmentResult(
        int id,
        int attemptId,
        CancellationToken cancellationToken)
    {
        if (id <= 0 || attemptId <= 0)
        {
            return NotFound();
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var hasDemoAccess = TempData.Peek($"DemoPayment:{id}") is not null;
        var hasCourseAccess = hasDemoAccess
            || await _coursePurchaseService.CanAccessCourseAsync(
                userId,
                id,
                cancellationToken);

        if (!hasCourseAccess)
        {
            return Forbid();
        }

        var attempt = await _courseAssessmentService.GetAttemptAsync(
            userId,
            id,
            attemptId,
            cancellationToken);

        return attempt is null ? NotFound() : View(attempt);
    }
}

