using CIITStackLab.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CIITStackLab.WebApp.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,Super User")]
public class ReportsController : Controller
{
    private const int MaxReportDays = 90;
    private readonly IAdminReportsService _reportsService;

    public ReportsController(IAdminReportsService reportsService)
    {
        _reportsService = reportsService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        DateTime? startDate,
        DateTime? endDate,
        CancellationToken cancellationToken)
    {
        var today = DateTime.Today;
        var selectedEnd = endDate?.Date ?? today;
        var selectedStart = startDate?.Date ?? selectedEnd.AddDays(-29);

        if (selectedStart > selectedEnd)
        {
            ModelState.AddModelError(string.Empty, "Start date cannot be after the end date.");
            selectedStart = selectedEnd.AddDays(-29);
        }

        if ((selectedEnd - selectedStart).TotalDays + 1 > MaxReportDays)
        {
            ModelState.AddModelError(
                string.Empty,
                $"Please select a date range of {MaxReportDays} days or less.");
            selectedStart = selectedEnd.AddDays(-(MaxReportDays - 1));
        }

        var overview = await _reportsService.GetOverviewAsync(
            selectedStart,
            selectedEnd,
            cancellationToken);

        return View(overview);
    }
}
