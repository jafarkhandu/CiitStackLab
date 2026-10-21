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
        var students = new List<AdminDashboardStudentDto>();

        if (!string.IsNullOrWhiteSpace(studentRoleId))
        {
            var studentQuery =
                from userRole in _dbContext.UserRoles.AsNoTracking()
                join user in _dbContext.Users.AsNoTracking()
                    on userRole.UserId equals user.Id
                where userRole.RoleId == studentRoleId
                      && user.IsActive
                select user;

            studentCount = await studentQuery
                .Select(x => x.Id)
                .Distinct()
                .CountAsync(cancellationToken);

            students = await studentQuery
                .GroupBy(x => x.Id)
                .Select(x => x.First())
                .OrderBy(x => x.UserName)
                .Select(x => new AdminDashboardStudentDto
                {
                    Id = x.Id,
                    UserName = x.UserName ?? string.Empty,
                    Email = x.Email ?? string.Empty
                })
                .ToListAsync(cancellationToken);
        }

        var activeCourses = await _dbContext.Courses
            .AsNoTracking()
            .Where(x => x.Flag == 0)
            .OrderBy(x => x.Title)
            .Select(x => new AdminDashboardCourseDto
            {
                Id = x.Id,
                Title = x.Title,
                UpdatedAt = x.UpdatedAt ?? x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var activeTopics = await _dbContext.Topics
            .AsNoTracking()
            .Where(x => x.Flag == 0)
            .OrderBy(x => x.Title)
            .Select(x => new AdminDashboardTopicDto
            {
                Id = x.Id,
                Title = x.Title,
                UpdatedAt = x.UpdatedAt ?? x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var learningContent = await _dbContext.Lessons
            .AsNoTracking()
            .Where(x => x.Flag == 0)
            .OrderBy(x => x.Title)
            .Select(x => new AdminDashboardContentDto
            {
                Id = x.Id,
                Title = x.Title,
                TopicId = x.TopicId,
                TopicTitle = x.Topic != null ? x.Topic.Title : string.Empty,
                UpdatedAt = x.UpdatedAt ?? x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var activeMcqs = await _dbContext.ContentQuestions
            .AsNoTracking()
            .Where(x => x.Flag == 0)
            .OrderBy(x => x.Id)
            .Select(x => new AdminDashboardMcqDto
            {
                Id = x.Id,
                Question = x.Question ?? string.Empty,
                ContentId = x.ContentId,
                TopicId = x.TopicId
            })
            .ToListAsync(cancellationToken);

        var recentCourses = activeCourses
            .OrderByDescending(x => x.UpdatedAt)
            .ThenByDescending(x => x.Id)
            .Take(5)
            .ToList();

        return new AdminDashboardDto
        {
            CourseCount = courseCount,
            TopicCount = topicCount,
            ContentCount = contentCount,
            McqCount = mcqCount,
            StudentCount = studentCount,
            RecentCourses = recentCourses,
            ActiveCourses = activeCourses,
            ActiveTopics = activeTopics,
            LearningContent = learningContent,
            ActiveMcqs = activeMcqs,
            Students = students
        };
    }
}
