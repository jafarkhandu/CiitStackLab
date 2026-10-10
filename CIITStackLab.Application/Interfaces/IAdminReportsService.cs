using CIITStackLab.Application.DTOs;

namespace CIITStackLab.Application.Interfaces;

public interface IAdminReportsService
{
    Task<AdminReportsOverviewDto> GetOverviewAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);
}
