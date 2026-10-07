using CIITStackLab.Application.Interfaces;
using CIITStackLab.WebApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CIITStackLab.WebApp.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,Super User")]
public class StudentsController : Controller
{
    private readonly IAdminStudentService _studentService;

    public StudentsController(IAdminStudentService studentService)
    {
        _studentService = studentService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var page = await _studentService.GetPageAsync(cancellationToken);

        return View(new AdminStudentIndexViewModel
        {
            ActiveStudents = page.ActiveStudents,
            InactiveStudents = page.InactiveStudents
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetStatus(
        string userId,
        bool isActive,
        CancellationToken cancellationToken)
    {
        var result = await _studentService.SetActiveAsync(
            userId,
            isActive,
            cancellationToken);

        TempData["AdminStudentMessage"] = result.Succeeded
            ? (isActive
                ? "Student account activated successfully."
                : "Student account deactivated successfully.")
            : result.Error ?? "Unable to update student account status.";

        TempData["AdminStudentMessageType"] =
            result.Succeeded ? "success" : "error";

        return RedirectToAction(nameof(Index));
    }
}
