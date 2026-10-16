using System.Security.Claims;
using CIITStackLab.Application.DTOs;
using CIITStackLab.Application.Interfaces;
using CIITStackLab.Domain.Entities;
using CIITStackLab.Infrastructure.Payments;
using CIITStackLab.Infrastructure.Persistence;
using CIITStackLab.Infrastructure.Services;
using CIITStackLab.WebApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CIITStackLab.WebApp.Controllers;

public class CoursesController : Controller
{
    private readonly ICourseService _courseService;
    private readonly IStudentProgressService _studentProgressService;
    private readonly ICourseAssessmentService _courseAssessmentService;
    private readonly CoursePurchaseService _coursePurchaseService;
    private readonly ApplicationDbContext _dbContext;
    private readonly IEmailService _emailService;
    private readonly PhonePePaymentService _phonePePaymentService;
    private readonly ILogger<CoursesController> _logger;

    public CoursesController(
        ICourseService courseService,
        IStudentProgressService studentProgressService,
        ICourseAssessmentService courseAssessmentService,
        CoursePurchaseService coursePurchaseService,
        ApplicationDbContext dbContext,
        IEmailService emailService,
        PhonePePaymentService phonePePaymentService,
        ILogger<CoursesController> logger)
    {
        _courseService = courseService;
        _studentProgressService = studentProgressService;
        _courseAssessmentService = courseAssessmentService;
        _coursePurchaseService = coursePurchaseService;
        _dbContext = dbContext;
        _emailService = emailService;
        _phonePePaymentService = phonePePaymentService;
        _logger = logger;
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
    public async Task<IActionResult> DemoPayment(
        int id,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            return NotFound();
        }

        var course = await _courseService.GetByIdAsync(id, cancellationToken);

        if (course is null)
        {
            return NotFound();
        }

        var price = await _coursePurchaseService.GetCoursePriceAsync(id, cancellationToken);

        if (price <= 0m)
        {
            return RedirectToAction(nameof(CourseHome));
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        if (await _coursePurchaseService.HasPurchasedCourseAsync(userId, id, cancellationToken))
        {
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

            TempData["CoursePaymentMessage"] = "Payment could not be started. Please try again.";
            return RedirectToAction(nameof(Details), new { id });
        }

        try
        {
            var amountInPaise = checked((long)Math.Round(price * 100m, 0, MidpointRounding.AwayFromZero));

            var checkoutUrl = await _phonePePaymentService.CreateCheckoutAsync(
                merchantOrderId,
                amountInPaise,
                redirectUrl);

            transaction.Status = "Pending";
            transaction.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            return Redirect(checkoutUrl);
        }
        catch (Exception ex)
        {
            transaction.Status = "Failed";
            transaction.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogError(
                ex,
                "PhonePe checkout could not be created for merchant order {MerchantOrderId}.",
                merchantOrderId);

            TempData["CoursePaymentMessage"] =
                "The payment gateway could not be reached. Please try again.";

            return RedirectToAction(nameof(Details), new { id });
        }
    }

    [HttpGet("Courses/PhonePeReturn")]
    [AllowAnonymous]
    public async Task<IActionResult> PhonePeReturn(
        string merchantOrderId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(merchantOrderId))
        {
            return RedirectToAction(nameof(CourseHome));
        }

        var transaction = await _dbContext.PaymentTransactions
            .FirstOrDefaultAsync(
                item => item.MerchantOrderId == merchantOrderId,
                cancellationToken);

        if (transaction is null)
        {
            TempData["CoursePaymentMessage"] = "The payment order could not be found.";
            return RedirectToAction(nameof(CourseHome));
        }

        try
        {
            var state = await _phonePePaymentService.GetOrderStateAsync(merchantOrderId);

            await ProcessPhonePeStateAsync(transaction, state, cancellationToken);

            if (string.Equals(state, "COMPLETED", StringComparison.OrdinalIgnoreCase))
            {
                TempData["CoursePaymentMessage"] =
                    "Payment successful. Your course is now available in My Courses.";
            }
            else if (string.Equals(state, "FAILED", StringComparison.OrdinalIgnoreCase))
            {
                TempData["CoursePaymentMessage"] =
                    "The payment was not completed. No course access was granted.";
            }
            else
            {
                TempData["CoursePaymentMessage"] =
                    "Your payment is still being processed. Please check My Courses again shortly.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "PhonePe status verification failed for merchant order {MerchantOrderId}.",
                merchantOrderId);

            TempData["CoursePaymentMessage"] =
                "We could not verify the payment yet. Please try again shortly.";
        }

        return RedirectToAction(nameof(Details), new { id = transaction.CourseId });
    }

    [HttpPost("Courses/PhonePeCallback")]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> PhonePeCallback(CancellationToken cancellationToken)
    {
        var authorization = Request.Headers.Authorization.ToString();

        if (string.IsNullOrWhiteSpace(authorization))
        {
            return Unauthorized();
        }

        using var reader = new StreamReader(Request.Body);
        var requestBody = await reader.ReadToEndAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(requestBody))
        {
            return BadRequest();
        }

        try
        {
            var callback = _phonePePaymentService.ValidateCallback(
                authorization,
                requestBody);

            if (callback.Payload is null)
            {
                return BadRequest();
            }

            var merchantOrderId = callback.Payload.OrderId;
            var state = callback.Payload.State;

            if (string.IsNullOrWhiteSpace(merchantOrderId)
                || string.IsNullOrWhiteSpace(state))
            {
                return BadRequest();
            }

            var transaction = await _dbContext.PaymentTransactions
                .FirstOrDefaultAsync(
                    item => item.MerchantOrderId == merchantOrderId,
                    cancellationToken);

            if (transaction is null)
            {
                return NotFound();
            }

            await ProcessPhonePeStateAsync(transaction, state, cancellationToken);

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Invalid PhonePe callback received.");
            return Unauthorized();
        }
    }

    private async Task ProcessPhonePeStateAsync(
        PaymentTransaction transaction,
        string state,
        CancellationToken cancellationToken)
    {
        if (string.Equals(state, "COMPLETED", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(transaction.Status, "Completed", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var alreadyPurchased = await _coursePurchaseService.HasPurchasedCourseAsync(
                transaction.UserId,
                transaction.CourseId,
                cancellationToken);

            var purchaseCompleted = await _coursePurchaseService.CompleteAsync(
                transaction.UserId,
                transaction.CourseId,
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
                await TrySendPurchaseInvoiceAsync(
                    transaction.UserId,
                    transaction.CourseId,
                    cancellationToken);
            }

            return;
        }

        if (string.Equals(state, "FAILED", StringComparison.OrdinalIgnoreCase))
        {
            transaction.Status = "Failed";
        }
        else
        {
            transaction.Status = "Pending";
        }

        transaction.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task TrySendPurchaseInvoiceAsync(
        string userId,
        int courseId,
        CancellationToken cancellationToken)
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

        if (purchase is null
            || course is null
            || string.IsNullOrWhiteSpace(user?.Email))
        {
            return;
        }

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

        var course = await _courseService.GetLearningAsync(id, null, cancellationToken);

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
        CancellationToken cancellationToken)
    {
        if (id <= 0 || (contentId.HasValue && contentId.Value <= 0))
        {
            return NotFound();
        }

        var publishedCourse = await _courseService.GetByIdAsync(id, cancellationToken);

        if (publishedCourse is null)
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

        var course = await _courseService.GetLearningAsync(
            id,
            contentId,
            cancellationToken);

        if (course is null)
        {
            return NotFound();
        }

        if (studentUserId is not null)
        {
            course.Progress = await _studentProgressService.GetProgressAsync(
                studentUserId,
                id,
                cancellationToken);
        }

        return View(course);
    }
}
