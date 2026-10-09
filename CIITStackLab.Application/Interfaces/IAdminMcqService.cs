using CIITStackLab.Application.DTOs;

namespace CIITStackLab.Application.Interfaces;

public interface IAdminMcqService
{
    Task<AdminMcqIndexDto> GetIndexAsync(CancellationToken cancellationToken = default);
    Task<AdminMcqDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdminMcqContentLookupDto>> GetContentLookupAsync(CancellationToken cancellationToken = default);
    Task<(bool Succeeded, string? Error)> CreateAsync(AdminMcqInputDto input, CancellationToken cancellationToken = default);
    Task<(bool Succeeded, string? Error)> UpdateAsync(int id, AdminMcqInputDto input, CancellationToken cancellationToken = default);
    Task<(bool Succeeded, string? Error)> SoftDeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<(bool Succeeded, string? Error)> RestoreAsync(int id, CancellationToken cancellationToken = default);
}
