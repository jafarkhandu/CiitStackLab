using CIITStackLab.Application.DTOs;

namespace CIITStackLab.Application.Interfaces;

public interface IStudentProgressService
{
    Task<int?> GetResumeContentIdAsync(
        string userId,
        int courseId,
        CancellationToken cancellationToken = default);

    Task<CourseLearningProgressDto> GetProgressAsync(
        string userId,
        int courseId,
        CancellationToken cancellationToken = default);

    Task<bool> RecordAccessAsync(
        string userId,
        int courseId,
        int contentId,
        CancellationToken cancellationToken = default);

    Task<bool> MarkCompletedAsync(
        string userId,
        int courseId,
        int contentId,
        CancellationToken cancellationToken = default);
}
