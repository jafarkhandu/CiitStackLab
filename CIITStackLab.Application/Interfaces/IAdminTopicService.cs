using CIITStackLab.Application.DTOs;

namespace CIITStackLab.Application.Interfaces;

public interface IAdminTopicService
{
    Task<IReadOnlyList<AdminTopicDto>> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdminArchivedTopicDto>> GetArchivedAsync(CancellationToken cancellationToken = default);
    Task<bool> TopicNameExistsAsync(string topicName, int? excludedTopicId = null, CancellationToken cancellationToken = default);
    Task<(bool Succeeded, string? Error)> CreateAsync(string topicName, string? publicFolderId, decimal price, int? durationMinutes, CancellationToken cancellationToken = default);
    Task<(bool Succeeded, string? Error)> UpdateAsync(int id, string topicName, string? publicFolderId, decimal price, int? durationMinutes, CancellationToken cancellationToken = default);
    Task<(bool Succeeded, string? Error)> DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<(bool Succeeded, string? Error)> RestoreAsync(int id, CancellationToken cancellationToken = default);
}
