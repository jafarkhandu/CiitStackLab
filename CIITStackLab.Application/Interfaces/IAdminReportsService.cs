using CIITStackLab.Application.DTOs;

namespace CIITStackLab.Application.Interfaces;

public interface IAdminReportsService
{
    Task<AdminReportsOverviewDto> GetOverviewAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);

    Task<AdminCourseReportPageDto> GetCourseReportsAsync(
        string? search,
        bool includeArchived,
        CancellationToken cancellationToken = default);

    Task<AdminStudentReportPageDto> GetStudentReportsAsync(
        string? search,
        string? status,
        CancellationToken cancellationToken = default);

    Task<AdminAssessmentReportPageDto> GetAssessmentReportsAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);

    Task<AdminRevenueReportPageDto> GetRevenueReportsAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);

    Task<AdminActivityReportPageDto> GetActivityReportsAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);
}
