using CIITStackLab.Application.DTOs;

namespace CIITStackLab.Application.Interfaces;

public interface INotesService
{
    Task<IReadOnlyList<AdminNoteDto>> GetAdminIndexAsync(CancellationToken cancellationToken = default);
    Task<AdminNoteDto?> GetAdminByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NoteTopicOptionDto>> GetTopicOptionsAsync(CancellationToken cancellationToken = default);
    Task<(bool Succeeded, string? Error)> CreateAsync(AdminNoteInputDto input, CancellationToken cancellationToken = default);
    Task<(bool Succeeded, string? Error)> UpdateAsync(int id, AdminNoteInputDto input, CancellationToken cancellationToken = default);
    Task<(bool Succeeded, string? Error)> DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<TopicNotesDto?> GetReaderAsync(int topicId, string? chapterId, CancellationToken cancellationToken = default);
}
