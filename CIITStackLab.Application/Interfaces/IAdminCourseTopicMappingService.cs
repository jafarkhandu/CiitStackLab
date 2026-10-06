using CIITStackLab.Application.DTOs;

namespace CIITStackLab.Application.Interfaces;

public interface IAdminCourseTopicMappingService
{
    Task<AdminCourseTopicMappingPageDto> GetPageAsync(
        int? selectedCourseId,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? Error)> SaveAsync(
        int courseId,
        IReadOnlyCollection<int> topicIds,
        CancellationToken cancellationToken = default);
}
