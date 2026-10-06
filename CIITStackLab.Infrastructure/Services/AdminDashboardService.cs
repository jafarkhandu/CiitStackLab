using CIITStackLab.Application.DTOs;
using CIITStackLab.Application.Interfaces;
using CIITStackLab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CIITStackLab.Infrastructure.Services;

public sealed class AdminDashboardService : IAdminDashboardService
{
    private readonly ApplicationDbContext _dbContext;

    public AdminDashboardService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AdminDashboardDto> GetOverviewAsync(
        CancellationToken cancellationToken = default)
    {
        // Keep database operations sequential because ApplicationDbContext is not
        // safe for concurrent database operations on the same scoped instance.
        var courseCount = await _dbContext.Courses
            .AsNoTracking()
            .CountAsync(x => x.Flag == 0, cancellationToken);

        var topicCount = await _dbContext.Topics
            .AsNoTracking()
            .CountAsync(x => x.Flag == 0, cancellationToken);

        var contentCount = await _dbContext.Lessons
            .AsNoTracking()
            .CountAsync(x => x.Flag == 0, cancellationToken);

        var mcqCount = await _dbContext.ContentQuestions
            .AsNoTracking()
            .CountAsync(x => x.Flag == 0, cancellationToken);

        var studentRoleId = await _dbContext.Roles
            .AsNoTracking()
            .Where(x => x.NormalizedName == "STUDENT")
            .Select(x => x.Id)
            .SingleOrDefaultAsync(cancellationToken);

        var studentCount = 0;

        if (!string.IsNullOrWhiteSpace(studentRoleId))
        {
            studentCount = await (
                from userRole in _dbContext.UserRoles.AsNoTracking()
                join user in _dbContext.Users.AsNoTracking()
                    on userRole.UserId equals user.Id
                where userRole.RoleId == studentRoleId
                      && user.IsActive
                select user.Id)
                .Distinct()
                .CountAsync(cancellationToken);
        }

        var recentCourses = await _dbContext.Courses
            .AsNoTracking()
            .Where(x => x.Flag == 0)
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Take(5)
            .Select(x => new AdminDashboardCourseDto
            {
                Id = x.Id,
                Title = x.Title,
                UpdatedAt = x.UpdatedAt ?? x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new AdminDashboardDto
        {
            CourseCount = courseCount,
            TopicCount = topicCount,
            ContentCount = contentCount,
            McqCount = mcqCount,
            StudentCount = studentCount,
            RecentCourses = recentCourses
        };
    }
}
