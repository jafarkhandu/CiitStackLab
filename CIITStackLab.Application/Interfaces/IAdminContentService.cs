using CIITStackLab.Application.DTOs;

namespace CIITStackLab.Application.Interfaces;

public interface IAdminContentService
{
    Task<AdminContentIndexDto> GetIndexAsync(CancellationToken cancellationToken = default);
    Task<AdminContentDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdminLookupDto>> GetCoursesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdminLookupDto>> GetTopicsForCourseAsync(int courseId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdminLookupDto>> GetTopicsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdminNoteLookupDto>> GetNotesForTopicAsync(int topicId, CancellationToken cancellationToken = default);
    Task<(bool Succeeded, string? Error)> CreateAsync(AdminContentInputDto input, CancellationToken cancellationToken = default);
    Task<(bool Succeeded, string? Error)> UpdateAsync(int id, AdminContentInputDto input, CancellationToken cancellationToken = default);
    Task<(bool Succeeded, string? Error)> SoftDeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<(bool Succeeded, string? Error)> RestoreAsync(int id, CancellationToken cancellationToken = default);
}
