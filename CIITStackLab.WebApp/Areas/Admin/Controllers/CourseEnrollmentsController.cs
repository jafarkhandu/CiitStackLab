using System.Security.Claims;
using CIITStackLab.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CIITStackLab.WebApp.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,Super User")]
public sealed class CourseEnrollmentsController : Controller
{
    private readonly ICourseEnrollmentService _enrollmentService;

    public CourseEnrollmentsController(ICourseEnrollmentService enrollmentService)
    {
        _enrollmentService = enrollmentService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var pending = await _enrollmentService.GetPendingAsync(cancellationToken);
        return View(pending);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Review(
        int enrollmentId,
        bool approve,
        CancellationToken cancellationToken)
    {
        if (enrollmentId <= 0)
        {
            return NotFound();
        }

        var reviewerUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(reviewerUserId))
        {
            return Unauthorized();
        }

        var succeeded = await _enrollmentService.ReviewAsync(
            enrollmentId,
            approve,
            reviewerUserId,
            cancellationToken);

        TempData["CourseEnrollmentAdminMessage"] = succeeded
            ? (approve
                ? "Enrollment approved. The student can now access the course."
                : "Enrollment request declined.")
            : "The request could not be updated. It may already have been reviewed.";

        TempData["CourseEnrollmentAdminMessageType"] =
            succeeded ? "success" : "error";

        return RedirectToAction(nameof(Index));
    }
}
