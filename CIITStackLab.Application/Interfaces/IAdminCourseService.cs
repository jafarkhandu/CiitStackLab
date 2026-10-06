using CIITStackLab.Application.DTOs;

namespace CIITStackLab.Application.Interfaces;

public interface IAdminCourseService
{
    Task<IReadOnlyList<AdminCourseDto>> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdminArchivedCourseDto>> GetArchivedAsync(CancellationToken cancellationToken = default);
    Task<bool> CourseNameExistsAsync(string courseName, int? excludedCourseId = null, CancellationToken cancellationToken = default);
    Task<(bool Succeeded, string? Error, AdminCourseDto? Course)> CreateAsync(
        string courseName,
        double? feesAmount,
        DateTime? feesChangeDate,
        double? installmentPercentage,
        CancellationToken cancellationToken = default);
    Task<(bool Succeeded, string? Error)> UpdateAsync(
        int id,
        string courseName,
        double? feesAmount,
        DateTime? feesChangeDate,
        double? installmentPercentage,
        CancellationToken cancellationToken = default);
    Task<(bool Succeeded, string? Error)> DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<(bool Succeeded, string? Error)> RestoreAsync(int id, CancellationToken cancellationToken = default);
}
