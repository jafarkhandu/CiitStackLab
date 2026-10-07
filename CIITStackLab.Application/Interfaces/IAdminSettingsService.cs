using CIITStackLab.Application.DTOs;

namespace CIITStackLab.Application.Interfaces;

public interface IAdminSettingsService
{
    Task<AdminSettingsDto?> GetAsync(string userId, CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? Error)> UpdateProfileAsync(
        string userId,
        AdminProfileSettingsInputDto input,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? Error)> ChangePasswordAsync(
        string userId,
        AdminChangePasswordInputDto input,
        CancellationToken cancellationToken = default);
}
