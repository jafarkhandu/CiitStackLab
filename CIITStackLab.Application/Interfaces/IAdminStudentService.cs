using CIITStackLab.Application.DTOs;

namespace CIITStackLab.Application.Interfaces;

public interface IAdminStudentService
{
    Task<AdminStudentPageDto> GetPageAsync(
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? Error)> SetActiveAsync(
        string userId,
        bool isActive,
        CancellationToken cancellationToken = default);
}
