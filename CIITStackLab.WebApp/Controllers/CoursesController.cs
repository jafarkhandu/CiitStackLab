using System.Security.Claims;
using CIITStackLab.Application.DTOs;
using CIITStackLab.Application.Interfaces;
using CIITStackLab.Infrastructure.Services;
using CIITStackLab.Infrastructure.Persistence;
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
    private readonly ILogger<CoursesController> _logger;

    public CoursesController(
        ICourseService courseService,
        IStudentProgressService studentProgressService,
        ICourseAssessmentService courseAssessmentService,
        CoursePurchaseService coursePurchaseService,
        ApplicationDbContext dbContext,
        IEmailService emailService,
        ILogger<CoursesController> logger)
    {
        _courseService = courseService;
        _studentProgressService = studentProgressService;
        _courseAssessmentService = courseAssessmentService;
        _coursePurchaseService = coursePurchaseService;
        _dbContext = dbContext;
        _emailService = emailService;
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

        var alreadyPurchased = await _coursePurchaseService.HasPurchasedCourseAsync(
            userId,
            id,
            cancellationToken);

        var purchaseCompleted = await _coursePurchaseService.CompleteAsync(
            userId,
            id,
            cancellationToken);

        if (!purchaseCompleted)
        {
            TempData["CoursePaymentMessage"] =
                "The demo payment could not be completed. Please try again.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (!alreadyPurchased)
        {
            var purchase = await _dbContext.StudentCoursePurchases
                .AsNoTracking()
                .Where(item => item.UserId == userId && item.CourseId == id)
                .OrderByDescending(item => item.PurchasedAt)
                .FirstOrDefaultAsync(cancellationToken);

            var user = await _dbContext.Users
                .AsNoTracking()
                .Where(item => item.Id == userId && item.IsActive)
                .Select(item => new { item.Email, item.UserName })
                .FirstOrDefaultAsync(cancellationToken);

            if (purchase is not null && !string.IsNullOrWhiteSpace(user?.Email))
            {
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
                        "Purchase email could not be sent for purchase {PurchaseId} to {Email}.",
                        purchase.Id,
                        user.Email);
                }
            }
        }

        TempData["CoursePaymentMessage"] =
            $"Demo payment successful. {course.Title} has been added to My Courses.";

        return RedirectToAction(nameof(CourseHome));
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

        if (!string.IsNullOrWhiteSpace(studentUserId) && !contentId.HasValue)
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

        var course = await _courseService.GetLearningAsync(id, contentId, cancellationToken);

        if (course is null)
        {
            return NotFound();
        }

        if (!string.IsNullOrWhiteSpace(studentUserId) && course.CurrentContentId.HasValue)
        {
            await _studentProgressService.RecordAccessAsync(
                studentUserId,
                id,
                course.CurrentContentId.Value,
                cancellationToken);

            course.Progress = await _studentProgressService.GetProgressAsync(
                studentUserId,
                id,
                cancellationToken);
        }

        return View(course);
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

    [HttpPost("Courses/Learn/{id:int}/Assessment/{contentId:int}")]
    [Authorize(Roles = "Student")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitAssessment(
        int id,
        int contentId,
        [FromForm] List<AssessmentAnswerDto> answers,
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
                "Payment is required before a paid-course assessment can be submitted.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var outcome = await _courseAssessmentService.SubmitAsync(
            userId,
            id,
            contentId,
            answers ?? new List<AssessmentAnswerDto>(),
            cancellationToken);

        if (!outcome.Succeeded || outcome.Result is null)
        {
            TempData["AssessmentError"] = outcome.ErrorMessage
                ?? "The assessment could not be submitted. Please try again.";

            return RedirectToAction(nameof(Learn), new { id, contentId });
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
