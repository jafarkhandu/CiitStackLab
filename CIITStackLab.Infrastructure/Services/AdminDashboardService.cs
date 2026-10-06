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
        var courseCountTask = _dbContext.Courses
            .AsNoTracking()
            .CountAsync(x => x.Flag == 0, cancellationToken);

        var topicCountTask = _dbContext.Topics
            .AsNoTracking()
            .CountAsync(x => x.Flag == 0, cancellationToken);

        var contentCountTask = _dbContext.Lessons
            .AsNoTracking()
            .CountAsync(x => x.Flag == 0, cancellationToken);

        var mcqCountTask = _dbContext.ContentQuestions
            .AsNoTracking()
            .CountAsync(x => x.Flag == 0, cancellationToken);

        var studentRoleIdTask = _dbContext.Roles
            .AsNoTracking()
            .Where(x => x.NormalizedName == "STUDENT")
            .Select(x => x.Id)
            .SingleOrDefaultAsync(cancellationToken);

        var recentCoursesTask = _dbContext.Courses
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

        await Task.WhenAll(
            courseCountTask,
            topicCountTask,
            contentCountTask,
            mcqCountTask,
            studentRoleIdTask,
            recentCoursesTask);

        var studentRoleId = await studentRoleIdTask;

        var studentCount = 0;
        if (!string.IsNullOrWhiteSpace(studentRoleId))
        {
            studentCount = await _dbContext.UserRoles
                .AsNoTracking()
                .CountAsync(x => x.RoleId == studentRoleId, cancellationToken);
        }

        return new AdminDashboardDto
        {
            CourseCount = await courseCountTask,
            TopicCount = await topicCountTask,
            ContentCount = await contentCountTask,
            McqCount = await mcqCountTask,
            StudentCount = studentCount,
            RecentCourses = await recentCoursesTask
        };
    }
}
