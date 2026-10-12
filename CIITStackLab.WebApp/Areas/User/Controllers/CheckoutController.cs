using CIITStackLab.Application.DTOs;
using CIITStackLab.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CIITStackLab.WebApp.Areas.User.Controllers;

[Area("User")]
[Authorize(Roles = "Student")]
public sealed class CheckoutController : Controller
{
    private readonly ICourseService _courseService;
    private readonly ICourseEnrollmentService _courseEnrollmentService;

    public CheckoutController(
        ICourseService courseService,
        ICourseEnrollmentService courseEnrollmentService)
    {
        _courseService = courseService;
        _courseEnrollmentService = courseEnrollmentService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
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

        if (course.TotalPrice <= 0m)
        {
            return RedirectToAction("Learn", "Courses", new { area = "", id });
        }

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        var enrollment = await _courseEnrollmentService.GetStudentEnrollmentAsync(
            userId,
            id,
            cancellationToken);

        if (enrollment?.HasAccess == true)
        {
            return RedirectToAction("Learn", "Courses", new { area = "", id });
        }

        course.IsStudent = true;
        course.Enrollment = enrollment;

        return View(course);
    }
}
