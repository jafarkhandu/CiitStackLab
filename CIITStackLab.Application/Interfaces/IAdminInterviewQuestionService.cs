using CIITStackLab.Application.DTOs;

namespace CIITStackLab.Application.Interfaces;

public interface IAdminInterviewQuestionService
{
    Task<AdminInterviewQuestionIndexDto> GetIndexAsync(CancellationToken cancellationToken = default);
    Task<AdminInterviewQuestionDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdminInterviewQuestionContentLookupDto>> GetContentLookupAsync(CancellationToken cancellationToken = default);
    Task<(bool Succeeded, string? Error)> CreateAsync(AdminInterviewQuestionInputDto input, CancellationToken cancellationToken = default);
    Task<(bool Succeeded, string? Error)> UpdateAsync(int id, AdminInterviewQuestionInputDto input, CancellationToken cancellationToken = default);
    Task<(bool Succeeded, string? Error)> SoftDeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<(bool Succeeded, string? Error)> RestoreAsync(int id, CancellationToken cancellationToken = default);
}
