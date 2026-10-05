using CIITStackLab.Application.DTOs;
using CIITStackLab.Application.Interfaces;
using CIITStackLab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CIITStackLab.Infrastructure.Services;

public sealed class CourseService : ICourseService
{
    private readonly ApplicationDbContext _dbContext;

    public CourseService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<CourseDto>> GetPublishedAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Courses
            .AsNoTracking()
            .Where(x => x.IsPublished)
            .OrderBy(x => x.Title)
            .Select(x => new CourseDto
            {
                Id = x.Id,
                Title = x.Title,
                ShortDescription = x.ShortDescription,
                Description = x.Description,
                ImageUrl = x.ImageUrl,
                Category = x.Category,
                Level = x.Level,
                DurationHours = x.DurationHours,
                IsPublished = x.IsPublished
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<CourseDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Courses
            .AsNoTracking()
            .Where(x => x.Id == id && x.IsPublished)
            .Select(x => new CourseDto
            {
                Id = x.Id,
                Title = x.Title,
                ShortDescription = x.ShortDescription,
                Description = x.Description,
                ImageUrl = x.ImageUrl,
                Category = x.Category,
                Level = x.Level,
                DurationHours = x.DurationHours,
                IsPublished = x.IsPublished
            })
            .SingleOrDefaultAsync(cancellationToken);
    }
}