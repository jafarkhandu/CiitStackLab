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
    private readonly IEmailService _emailService;

    public StudentExaminationsController(
        ITopicExaminationService exams,
        ApplicationDbContext db,
        ExamApplicationNotificationService examApplicationNotificationService,
        PhonePePaymentService phonePePaymentService,
        CoursePurchaseService coursePurchaseService,
        IEmailService emailService,
        ILogger<StudentExaminationsController> logger)
    {
        _exams = exams;
        _db = db;
        _examApplicationNotificationService = examApplicationNotificationService;
        _phonePePaymentService = phonePePaymentService;
        _coursePurchaseService = coursePurchaseService;
        _emailService = emailService;
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
            TempData["OpenExamApplicationModal"] = true;
            return RedirectToAction("Learn", "Courses", new { id = request.CourseId, topicId = request.TopicId });
        }
        await SendExamLifecycleEmailSafelyAsync(
            userId,
            result.Exam!.CourseId,
            result.Exam.TopicId,
            result.Exam.AttemptId,
            "Scheduled",
            cancellationToken);
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
            TempData["OpenExamApplicationModal"] = true;
            return RedirectToAction("Learn", "Courses", new { id = request.CourseId, topicId = request.TopicId });
        }
        await SendExamLifecycleEmailSafelyAsync(
            userId,
            result.Exam.CourseId,
            result.Exam.TopicId,
            result.Exam.AttemptId,
            "Scheduled",
            cancellationToken);
        return RedirectToAction(nameof(Instructions), new { attemptId = result.Exam.AttemptId });
    }

    [HttpPost("ReExam")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReExam(int courseId, int topicId, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();

        // A retake is a new attempt; the examination service enforces the free-attempt limit
        // and consumes a verified, matching payment for every later attempt.
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

        await SendExamLifecycleEmailSafelyAsync(
            userId,
            result.Exam.CourseId,
            result.Exam.TopicId,
            result.Exam.AttemptId,
            "Scheduled",
            cancellationToken);
        return RedirectToAction(nameof(Instructions), new { attemptId = result.Exam.AttemptId });
    }

    [HttpPost("PayForReExam")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PayForReExam(int courseId, int topicId, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();
        if (courseId <= 0 || topicId <= 0) return NotFound();

        var validTopic = await _db.CourseModules.AsNoTracking().AnyAsync(module =>
            module.CourseId == courseId && module.TopicId == topicId && module.Flag == 0
            && module.Course.Flag == 0 && module.Topic.Flag == 0, cancellationToken);
        if (!validTopic) return NotFound();

        if (!await _coursePurchaseService.CanAccessCourseAsync(userId, courseId, cancellationToken))
        {
            TempData["ExamError"] = "You do not have access to this course.";
            TempData["OpenExamApplicationModal"] = true;
            return RedirectToAction("Learn", "Courses", new { id = courseId, topicId });
        }

        var orderedTopicIds = await _db.CourseModules.AsNoTracking()
            .Where(module => module.CourseId == courseId && module.Flag == 0
                && module.Course.Flag == 0 && module.Topic.Flag == 0)
            .OrderBy(module => module.Id)
            .Select(module => module.TopicId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var requestedTopicIndex = orderedTopicIds.IndexOf(topicId);
        var currentTopicId = await _exams.GetCurrentTopicIdAsync(userId, courseId, cancellationToken);
        var currentTopicIndex = currentTopicId.HasValue ? orderedTopicIds.IndexOf(currentTopicId.Value) : -1;
        if (requestedTopicIndex < 0
            || currentTopicIndex >= 0 && requestedTopicIndex > currentTopicIndex)
        {
            TempData["ExamError"] = "Please first clear your previous topic exam.";
            TempData["OpenExamApplicationModal"] = true;
            return RedirectToAction("Learn", "Courses", new { id = courseId, topicId });
        }
        if (currentTopicIndex >= 0 && requestedTopicIndex < currentTopicIndex)
        {
            var laterTopicIds = orderedTopicIds.Skip(requestedTopicIndex + 1).ToList();
            if (laterTopicIds.Count > 0 && await _db.TopicExamAttempts.AsNoTracking().AnyAsync(attempt =>
                attempt.UserId == userId && attempt.CourseId == courseId
                && laterTopicIds.Contains(attempt.TopicId), cancellationToken))
            {
                TempData["ExamError"] = "You can no longer apply for a previous topic exam after moving on to a later topic exam.";
                TempData["OpenExamApplicationModal"] = true;
                return RedirectToAction("Learn", "Courses", new { id = courseId, topicId });
            }
        }
        if (requestedTopicIndex > 0)
        {
            foreach (var previousTopicId in orderedTopicIds.Take(requestedTopicIndex))
            {
                var priorPassExists = await _db.TopicExamAttempts.AsNoTracking().AnyAsync(attempt =>
                    attempt.UserId == userId && attempt.CourseId == courseId
                    && attempt.TopicId == previousTopicId && attempt.Passed && attempt.Status == "Passed",
                    cancellationToken);
                if (!priorPassExists) continue;
                var priorLatest = await _db.TopicExamAttempts.AsNoTracking()
                    .Where(attempt => attempt.UserId == userId && attempt.CourseId == courseId
                        && attempt.TopicId == previousTopicId)
                    .OrderByDescending(attempt => attempt.Id)
                    .Select(attempt => attempt.Status)
                    .FirstOrDefaultAsync(cancellationToken);
                if (priorLatest is "Scheduled" or "Ready" or "InProgress")
                {
                    TempData["ExamError"] = "Please first clear your previous topic exam.";
                    TempData["OpenExamApplicationModal"] = true;
                    return RedirectToAction("Learn", "Courses", new { id = courseId, topicId });
                }
            }
        }

        var activeAttemptExists = await _db.TopicExamAttempts.AnyAsync(attempt =>
            attempt.UserId == userId && attempt.CourseId == courseId && attempt.TopicId == topicId
            && (attempt.Status == "Scheduled" || attempt.Status == "Ready" || attempt.Status == "InProgress"),
            cancellationToken);
        if (activeAttemptExists)
        {
            TempData["ExamError"] = "An exam is already scheduled or active for this topic.";
            TempData["OpenExamApplicationModal"] = true;
            return RedirectToAction("Learn", "Courses", new { id = courseId, topicId });
        }

        var questionAvailability = await _exams.CheckQuestionAvailabilityAsync(
            userId, courseId, topicId, cancellationToken);
        if (!questionAvailability.IsAvailable)
        {
            TempData["ExamError"] = questionAvailability.Error
                ?? "Exam questions are not available for this topic right now.";
            await NotifyIfQuestionBankUnavailableAsync(
                userId, courseId, topicId, questionAvailability.Error, cancellationToken);
            TempData["OpenExamApplicationModal"] = true;
            return RedirectToAction("Learn", "Courses", new { id = courseId, topicId });
        }

        var attemptNumber = await _db.TopicExamAttempts.CountAsync(attempt =>
            attempt.UserId == userId && attempt.CourseId == courseId && attempt.TopicId == topicId,
            cancellationToken) + 1;
        var hasEverPassedTopic = await _db.TopicExamAttempts.AsNoTracking().AnyAsync(attempt =>
            attempt.UserId == userId && attempt.CourseId == courseId && attempt.TopicId == topicId
            && attempt.Passed && attempt.Status == "Passed", cancellationToken);
        if (attemptNumber <= 2 && !hasEverPassedTopic)
        {
            TempData["ExamError"] = "This attempt is free. Start or schedule it from the examination application.";
            TempData["OpenExamApplicationModal"] = true;
            return RedirectToAction("Learn", "Courses", new { id = courseId, topicId });
        }

        var existingUnusedPayment = await _db.PaymentTransactions.AnyAsync(payment =>
            payment.UserId == userId && payment.CourseId == courseId
            && payment.PaymentPurpose == "ExamRetake" && payment.ExamTopicId == topicId
            && payment.ExamAttemptNumber == attemptNumber
            && payment.Status == "Completed" && payment.ExamAttemptId == null,
            cancellationToken);
        if (existingUnusedPayment)
        {
            TempData["ExamMessage"] = "Payment is already confirmed. You can now schedule or start this re-exam.";
            TempData["OpenExamApplicationModal"] = true;
            return RedirectToAction("Learn", "Courses", new { id = courseId, topicId });
        }

        var pendingPayment = await _db.PaymentTransactions.AnyAsync(payment =>
            payment.UserId == userId && payment.CourseId == courseId
            && payment.PaymentPurpose == "ExamRetake" && payment.ExamTopicId == topicId
            && payment.ExamAttemptNumber == attemptNumber
            && (payment.Status == "Created" || payment.Status == "Pending"),
            cancellationToken);
        if (pendingPayment)
        {
            TempData["ExamError"] = "A payment order for this attempt is already pending. Complete or verify that order before creating another.";
            TempData["OpenExamApplicationModal"] = true;
            return RedirectToAction("Learn", "Courses", new { id = courseId, topicId });
        }

        var baseFee = Math.Max(1, attemptNumber - 2) * 100m;
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
            "PhonePeReturn",
            "Courses",
            new { merchantOrderId },
            Request.Scheme,
            Request.Host.Value);
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            transaction.Status = "Failed";
            transaction.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            TempData["ExamError"] = "Payment could not be started. Please try again.";
            TempData["OpenExamApplicationModal"] = true;
            return RedirectToAction("Learn", "Courses", new { id = courseId, topicId });
        }

        try
        {
            var amountInPaise = checked((long)Math.Round(totalAmount * 100m, 0, MidpointRounding.AwayFromZero));
            var checkoutUrl = await _phonePePaymentService.CreateCheckoutAsync(merchantOrderId, amountInPaise, returnUrl);
            transaction.Status = "Pending";
            transaction.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            return Redirect(checkoutUrl);
        }
        catch (Exception ex)
        {
            transaction.Status = ex is ArgumentException or InvalidOperationException ? "Failed" : "Pending";
            transaction.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(CancellationToken.None);
            _logger.LogError(ex, "PhonePe exam retake checkout could not be created for order {MerchantOrderId}.", merchantOrderId);
            TempData["ExamError"] = transaction.Status == "Pending"
                ? "The payment gateway response could not be confirmed. The order remains pending and no attempt has been unlocked."
                : "The payment could not be started. Please check the payment configuration and try again.";
            TempData["OpenExamApplicationModal"] = true;
            return RedirectToAction("Learn", "Courses", new { id = courseId, topicId });
        }
    }

    private async Task SendExamLifecycleEmailSafelyAsync(
        string userId,
        int courseId,
        int topicId,
        long attemptId,
        string eventType,
        CancellationToken cancellationToken)
    {
        try
        {
            var user = await _db.Users.AsNoTracking()
                .Where(item => item.Id == userId)
                .Select(item => new { item.Email, item.UserName })
                .FirstOrDefaultAsync(cancellationToken);
            if (user is null || string.IsNullOrWhiteSpace(user.Email))
            {
                _logger.LogWarning(
                    "Exam {EventType} email was not sent because the student email is unavailable. Attempt {AttemptId}.",
                    eventType,
                    attemptId);
                return;
            }

            var email = user.Email;
            var courseTitle = await _db.Courses.AsNoTracking()
                .Where(course => course.Id == courseId)
                .Select(course => course.Title)
                .FirstOrDefaultAsync(cancellationToken) ?? "Course";
            var topicTitle = await _db.Topics.AsNoTracking()
                .Where(topic => topic.Id == topicId)
                .Select(topic => topic.Title)
                .FirstOrDefaultAsync(cancellationToken) ?? "Topic";
            var attempt = await _db.TopicExamAttempts.AsNoTracking()
                .Where(item => item.Id == attemptId && item.UserId == userId)
                .Select(item => new { item.ScheduledAtUtc })
                .FirstOrDefaultAsync(cancellationToken);
            if (attempt is null)
            {
                _logger.LogWarning("Exam lifecycle email skipped because attempt {AttemptId} was not found.", attemptId);
                return;
            }

            var attemptNumber = await _db.TopicExamAttempts.AsNoTracking()
                .CountAsync(item => item.UserId == userId
                    && item.CourseId == courseId
                    && item.TopicId == topicId
                    && (item.ScheduledAtUtc < attempt.ScheduledAtUtc
                        || (item.ScheduledAtUtc == attempt.ScheduledAtUtc && item.Id <= attemptId)),
                    cancellationToken);
            var payment = await _db.PaymentTransactions.AsNoTracking()
                .Where(item => item.UserId == userId
                    && item.CourseId == courseId
                    && item.PaymentPurpose == "ExamRetake"
                    && item.ExamAttemptId == attemptId
                    && item.Status == "Completed")
                .Select(item => new { item.Amount, item.Status, item.MerchantOrderId })
                .FirstOrDefaultAsync(cancellationToken);

            await _emailService.SendExamLifecycleEmailAsync(
                email,
                user.UserName ?? "Student",
                courseTitle,
                topicTitle,
                attemptNumber,
                attempt.ScheduledAtUtc,
                eventType,
                payment?.Amount,
                payment?.Status ?? "Not required",
                payment?.MerchantOrderId,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Could not send exam {EventType} email for attempt {AttemptId}.",
                eventType,
                attemptId);
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
        var startedAttempt = await _db.TopicExamAttempts.AsNoTracking()
            .Where(attempt => attempt.Id == result.Session.AttemptId && attempt.UserId == userId)
            .Select(attempt => new { attempt.CourseId, attempt.TopicId })
            .FirstOrDefaultAsync(cancellationToken);
        if (startedAttempt is not null)
        {
            await SendExamLifecycleEmailSafelyAsync(
                userId,
                startedAttempt.CourseId,
                startedAttempt.TopicId,
                result.Session.AttemptId,
                "Started",
                cancellationToken);
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
        // Device verification is also required for event logging; answer submission is scoped to the authenticated owner.
        var result = await _exams.SaveAnswersAsync(
            userId, attemptId, answers, deviceToken, cancellationToken, finalSubmission);
        if (!result.Succeeded) return BadRequest(new { error = result.Error });

        if (finalSubmission && result.Result is not null)
        {
            TempData["LastExamResultStatus"] = result.Result.Status;
            TempData["LastExamResultScore"] = result.Result.Score;
            TempData["LastExamResultTotal"] = result.Result.TotalQuestions;
            TempData["LastExamResultAnswered"] = result.Result.AnsweredQuestionsCount;
            TempData["LastExamResultPassed"] = result.Result.Passed;
            TempData["LastExamResultAttemptId"] = result.Result.AttemptId;
        }

        return Json(new
        {
            success = true,
            status = result.Result?.Status,
            score = result.Result?.Score,
            totalQuestions = result.Result?.TotalQuestions,
            answeredQuestions = result.Result?.AnsweredQuestionsCount,
            passed = result.Result?.Passed
        });
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
