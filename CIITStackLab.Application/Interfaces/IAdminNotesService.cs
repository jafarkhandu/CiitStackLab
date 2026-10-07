using CIITStackLab.Application.DTOs;

namespace CIITStackLab.Application.Interfaces;

public interface IAdminNotesService
{
    Task<IReadOnlyList<AdminNotesDto>> GetIndexAsync(CancellationToken cancellationToken = default);
    Task<AdminNotesDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<(bool Succeeded, string? Error)> UpdateHtmlContentAsync(
        int id,
        AdminNotesInputDto input,
        CancellationToken cancellationToken = default);
}
