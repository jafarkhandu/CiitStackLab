using CIITStackLab.Application.DTOs;

namespace CIITStackLab.Application.Interfaces;

public interface IAdminPracticeProgramService
{
    Task<AdminPracticeProgramIndexDto> GetIndexAsync(CancellationToken cancellationToken = default);
    Task<AdminPracticeProgramDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<AdminPracticeProgramAnswerDto?> GetAnswerByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdminPracticeProgramContentLookupDto>> GetContentLookupAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdminPracticeProgramLookupDto>> GetProgramLookupAsync(CancellationToken cancellationToken = default);
    Task<AdminOperationResult> CreateAsync(AdminPracticeProgramInputDto input, CancellationToken cancellationToken = default);
    Task<AdminOperationResult> UpdateAsync(int id, AdminPracticeProgramInputDto input, CancellationToken cancellationToken = default);
    Task<AdminOperationResult> SoftDeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<AdminOperationResult> RestoreAsync(int id, CancellationToken cancellationToken = default);
    Task<AdminOperationResult> CreateAnswerAsync(AdminPracticeProgramAnswerInputDto input, CancellationToken cancellationToken = default);
    Task<AdminOperationResult> UpdateAnswerAsync(int id, AdminPracticeProgramAnswerInputDto input, CancellationToken cancellationToken = default);
    Task<AdminOperationResult> SoftDeleteAnswerAsync(int id, CancellationToken cancellationToken = default);
    Task<AdminOperationResult> RestoreAnswerAsync(int id, CancellationToken cancellationToken = default);
}
