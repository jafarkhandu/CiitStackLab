using CIITStackLab.Application.DTOs;

namespace CIITStackLab.Application.Interfaces;

public interface IAdminDashboardService
{
    Task<AdminDashboardDto> GetOverviewAsync(
        CancellationToken cancellationToken = default);
}
