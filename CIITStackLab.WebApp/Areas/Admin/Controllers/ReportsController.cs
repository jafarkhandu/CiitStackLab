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
    public async Task<IActionResult> Index(DateTime? startDate, DateTime? endDate, CancellationToken cancellationToken)
    {
        var (start, end) = ResolveDateRange(startDate, endDate);
        var overview = await _reportsService.GetOverviewAsync(start, end, cancellationToken);
        return View(overview);
    }

    [HttpGet]
    public async Task<IActionResult> CourseReports(string? search, bool includeArchived, CancellationToken cancellationToken)
    {
        var model = await _reportsService.GetCourseReportsAsync(search, includeArchived, cancellationToken);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> StudentReports(string? search, string? status, CancellationToken cancellationToken)
    {
        var model = await _reportsService.GetStudentReportsAsync(search, status, cancellationToken);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> AssessmentReports(DateTime? startDate, DateTime? endDate, CancellationToken cancellationToken)
    {
        var (start, end) = ResolveDateRange(startDate, endDate);
        var model = await _reportsService.GetAssessmentReportsAsync(start, end, cancellationToken);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> RevenuePayments(DateTime? startDate, DateTime? endDate, CancellationToken cancellationToken)
    {
        var (start, end) = ResolveDateRange(startDate, endDate);
        var model = await _reportsService.GetRevenueReportsAsync(start, end, cancellationToken);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> ActivityReports(DateTime? startDate, DateTime? endDate, CancellationToken cancellationToken)
    {
        var (start, end) = ResolveDateRange(startDate, endDate);
        var model = await _reportsService.GetActivityReportsAsync(start, end, cancellationToken);
        return View(model);
    }

    private static (DateTime Start, DateTime End) ResolveDateRange(DateTime? startDate, DateTime? endDate)
    {
        var today = DateTime.Today;
        var end = endDate?.Date ?? today;
        var start = startDate?.Date ?? end.AddDays(-29);

        if (start > end)
        {
            (start, end) = (end.AddDays(-29), end);
        }

        if ((end - start).TotalDays + 1 > MaxReportDays)
        {
            start = end.AddDays(-(MaxReportDays - 1));
        }

        return (start, end);
    }
}
