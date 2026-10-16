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
    private readonly IMuxVideoService _muxVideoService;
    private readonly ApplicationDbContext _dbContext;
    private readonly ITopicExaminationService _topicExaminationService;

    public CoursesController(
        ICourseService courseService,
        IStudentProgressService studentProgressService,
        ICourseAssessmentService courseAssessmentService,
        CoursePurchaseService coursePurchaseService,
        IMuxVideoService muxVideoService,
        ApplicationDbContext dbContext,
        ITopicExaminationService topicExaminationService)
    {
        _courseService = courseService;
        _studentProgressService = studentProgressService;
        _courseAssessmentService = courseAssessmentService;
        _coursePurchaseService = coursePurchaseService;
        _muxVideoService = muxVideoService;
        _dbContext = dbContext;
        _topicExaminationService = topicExaminationService;
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
        int? topicId,
        CancellationToken cancellationToken)
    {
        if (id <= 0
            || (contentId.HasValue && contentId.Value <= 0)
            || (topicId.HasValue && topicId.Value <= 0))
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

        if (!string.IsNullOrWhiteSpace(studentUserId)
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

        var course = await _courseService.GetLearningAsync(id, contentId, cancellationToken);

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

            if (allowedTopicId.HasValue && requestedTopicId.HasValue && requestedTopicId.Value != allowedTopicId.Value)
            {
                TempData["LearningProgressMessage"] = "This topic is locked until you pass the current topic examination.";
                return RedirectToAction(nameof(Learn), new { id, topicId = allowedTopicId.Value });
            }
        }

        if (topicId.HasValue)
        {
            var selectedTopic = course.Topics.FirstOrDefault(topic => topic.Id == topicId.Value);
            if (selectedTopic is null)
            {
                return NotFound();
            }

            var topicContents = selectedTopic.Contents
                .Concat(selectedTopic.Chapters.SelectMany(chapter => chapter.Contents))
                .DistinctBy(content => content.Id)
                .ToList();

            if (!contentId.HasValue && topicContents.Count > 0)
            {
                var selectedContentId = topicContents[0].Id;

                if (!string.IsNullOrWhiteSpace(studentUserId))
                {
                    var resumeContentId = await _studentProgressService.GetResumeContentIdAsync(
                        studentUserId,
                        id,
                        cancellationToken);

                    if (resumeContentId.HasValue
                        && topicContents.Any(content => content.Id == resumeContentId.Value))
                    {
                        selectedContentId = resumeContentId.Value;
                    }
                }

                course = await _courseService.GetLearningAsync(id, selectedContentId, cancellationToken);
                if (course is null)
                {
                    return NotFound();
                }

                selectedTopic = course.Topics.FirstOrDefault(topic => topic.Id == topicId.Value);
                if (selectedTopic is null)
                {
                    return NotFound();
                }
            }

            course.CurrentTopicId = selectedTopic.Id;
        }

        if (!string.IsNullOrWhiteSpace(studentUserId))
        {
            if (course.CurrentContentId.HasValue)
            {
                await _studentProgressService.RecordAccessAsync(
                    studentUserId,
                    id,
                    course.CurrentContentId.Value,
                    cancellationToken);
            }

            course.Progress = await _studentProgressService.GetProgressAsync(
                studentUserId,
                id,
                cancellationToken);

            var examTopicId = course.CurrentTopicId ?? topicId;
            if (examTopicId.HasValue)
            {
                var examTopic = course.Topics.FirstOrDefault(t => t.Id == examTopicId.Value);
                var topicContentIds = examTopic is null
                    ? new List<int>()
                    : examTopic.Contents.Concat(examTopic.Chapters.SelectMany(chapter => chapter.Contents))
                        .Select(content => content.Id).Distinct().ToList();
                var topicCompleted = topicContentIds.Count > 0
                    && topicContentIds.All(course.Progress.CompletedContentIds.Contains);
                var hasPendingOrPassedExam = await _dbContext.TopicExamAttempts.AsNoTracking().AnyAsync(
                    attempt => attempt.UserId == studentUserId
                        && attempt.CourseId == id && attempt.TopicId == examTopicId.Value
                        && (attempt.Status == "Scheduled" || attempt.Status == "Ready"
                            || attempt.Status == "InProgress" || attempt.Status == "Passed"),
                    cancellationToken);
                ViewBag.ShowExamApplicationModal = topicCompleted && !hasPendingOrPassedExam;
                ViewBag.ExamApplicationTopicId = examTopicId.Value;
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
