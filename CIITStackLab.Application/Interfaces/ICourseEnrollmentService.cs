using CIITStackLab.Application.DTOs;

namespace CIITStackLab.Application.Interfaces;

public interface ICourseEnrollmentService
{
    Task<CourseEnrollmentDto?> GetStudentEnrollmentAsync(
        string userId,
        int courseId,
        CancellationToken cancellationToken = default);

    Task<bool> HasActiveEnrollmentAsync(
        string userId,
        int courseId,
        CancellationToken cancellationToken = default);

    Task<CourseEnrollmentRequestResultDto> RequestEnrollmentAsync(
        string userId,
        int courseId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PendingCourseEnrollmentDto>> GetPendingAsync(
        CancellationToken cancellationToken = default);

    Task<bool> ReviewAsync(
        int enrollmentId,
        bool approve,
        string reviewerUserId,
        CancellationToken cancellationToken = default);
}
