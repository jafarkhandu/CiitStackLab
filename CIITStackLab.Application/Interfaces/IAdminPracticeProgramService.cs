using CIITStackLab.Application.DTOs;

namespace CIITStackLab.Application.Interfaces;

public interface IAdminPracticeProgramService
{
    Task<AdminPracticeProgramIndexDto> GetIndexAsync(CancellationToken cancellationToken = default);
    Task<AdminPracticeProgramDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<AdminPracticeProgramAnswerDto?> GetAnswerByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdminPracticeProgramContentLookupDto>> GetContentLookupAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdminPracticeProgramLookupDto>> GetProgramLookupAsync(CancellationToken cancellationToken = default);
    Task<(bool Succeeded, string? Error)> CreateAsync(AdminPracticeProgramInputDto input, CancellationToken cancellationToken = default);
    Task<(bool Succeeded, string? Error)> UpdateAsync(int id, AdminPracticeProgramInputDto input, CancellationToken cancellationToken = default);
    Task<(bool Succeeded, string? Error)> SoftDeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<(bool Succeeded, string? Error)> RestoreAsync(int id, CancellationToken cancellationToken = default);
    Task<(bool Succeeded, string? Error)> CreateAnswerAsync(AdminPracticeProgramAnswerInputDto input, CancellationToken cancellationToken = default);
    Task<(bool Succeeded, string? Error)> UpdateAnswerAsync(int id, AdminPracticeProgramAnswerInputDto input, CancellationToken cancellationToken = default);
    Task<(bool Succeeded, string? Error)> SoftDeleteAnswerAsync(int id, CancellationToken cancellationToken = default);
    Task<(bool Succeeded, string? Error)> RestoreAnswerAsync(int id, CancellationToken cancellationToken = default);
}
