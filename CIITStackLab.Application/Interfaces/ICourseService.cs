using CIITStackLab.Application.DTOs;

namespace CIITStackLab.Application.Interfaces;

public interface ICourseService
{
    Task<IReadOnlyList<CourseDto>> GetPublishedAsync(CancellationToken cancellationToken = default);
    Task<CourseDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
}