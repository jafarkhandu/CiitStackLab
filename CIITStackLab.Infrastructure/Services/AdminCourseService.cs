using CIITStackLab.Application.DTOs;
using CIITStackLab.Application.Interfaces;
using CIITStackLab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CIITStackLab.Infrastructure.Services;

public sealed class AdminCourseService : IAdminCourseService
{
    private readonly ApplicationDbContext _dbContext;

    public AdminCourseService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<AdminCourseDto>> GetActiveAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Courses
            .AsNoTracking()
            .Where(x => x.Flag == 0)
            .OrderByDescending(x => x.Id)
            .Select(x => new AdminCourseDto
            {
                Id = x.Id,
                Title = x.Title,
                FeesAmount = x.FeesAmount,
                FeesChangeDate = x.FeesChangeDate,
                InstallmentPercentage = x.InstallmentPercentage,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AdminArchivedCourseDto>> GetArchivedAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Courses
            .AsNoTracking()
            .Where(x => x.Flag == 1)
            .OrderByDescending(x => x.Id)
            .Select(x => new AdminArchivedCourseDto
            {
                Id = x.Id,
                Title = x.Title,
                DeletedAt = x.DeletedAt
            })
            .ToListAsync(cancellationToken);
    }

    public Task<bool> CourseNameExistsAsync(
        string courseName,
        int? excludedCourseId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Courses
            .AsNoTracking()
            .Where(x => x.Flag == 0 && x.Title == courseName);

        if (excludedCourseId.HasValue)
        {
            query = query.Where(x => x.Id != excludedCourseId.Value);
        }

        return query.AnyAsync(cancellationToken);
    }

    public async Task<(bool Succeeded, string? Error, AdminCourseDto? Course)> CreateAsync(
        string courseName,
        double? feesAmount,
        DateTime? feesChangeDate,
        double? installmentPercentage,
        CancellationToken cancellationToken = default)
    {
        var course = new Domain.Entities.Course
        {
            Title = courseName,
            FeesAmount = feesAmount,
            FeesChangeDate = feesChangeDate?.Date,
            InstallmentPercentage = installmentPercentage,
            Flag = 0,
            CreatedAt = DateTime.Now
        };

        _dbContext.Courses.Add(course);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            _dbContext.Entry(course).State = EntityState.Detached;
            return (false, "The course could not be created. The name may already exist.", null);
        }

        return (true, null, new AdminCourseDto
        {
            Id = course.Id,
            Title = course.Title,
            FeesAmount = course.FeesAmount,
            FeesChangeDate = course.FeesChangeDate,
            InstallmentPercentage = course.InstallmentPercentage,
            CreatedAt = course.CreatedAt,
            UpdatedAt = course.UpdatedAt
        });
    }

    public async Task<(bool Succeeded, string? Error)> UpdateAsync(
        int id,
        string courseName,
        double? feesAmount,
        DateTime? feesChangeDate,
        double? installmentPercentage,
        CancellationToken cancellationToken = default)
    {
        var course = await _dbContext.Courses
            .SingleOrDefaultAsync(x => x.Id == id && x.Flag == 0, cancellationToken);

        if (course is null)
        {
            return (false, "The selected course was not found.");
        }

        course.Title = courseName;
        course.FeesAmount = feesAmount;
        course.FeesChangeDate = feesChangeDate?.Date;
        course.InstallmentPercentage = installmentPercentage;
        course.UpdatedAt = DateTime.Now;

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return (false, "The course could not be updated. The name may already exist.");
        }

        return (true, null);
    }

    public async Task<(bool Succeeded, string? Error)> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var course = await _dbContext.Courses
            .SingleOrDefaultAsync(x => x.Id == id && x.Flag == 0, cancellationToken);

        if (course is null)
        {
            return (false, "The selected course was not found.");
        }

        course.Flag = 1;
        course.DeletedAt = DateTime.Now;
        course.UpdatedAt = DateTime.Now;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<(bool Succeeded, string? Error)> RestoreAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var course = await _dbContext.Courses
            .SingleOrDefaultAsync(x => x.Id == id && x.Flag == 1, cancellationToken);

        if (course is null)
        {
            return (false, "The selected archived course was not found.");
        }

        course.Flag = 0;
        course.DeletedAt = null;
        course.RestoredAt = DateTime.Now;
        course.UpdatedAt = DateTime.Now;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return (true, null);
    }
}
