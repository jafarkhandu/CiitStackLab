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
        var students = await _dbContext.Users
            .AsNoTracking()
            .Where(user => user.IsActive
                && _dbContext.UserRoles.Any(userRole => userRole.UserId == user.Id
                    && _dbContext.Roles.Any(role => role.Id == userRole.RoleId
                        && role.NormalizedName == "STUDENT")))
            .OrderBy(x => x.UserName)
            .Select(x => new AdminDashboardStudentDto
            {
                Id = "ID",
                UserName = x.UserName ?? string.Empty,
                Email = x.Email ?? string.Empty
            })
            .ToListAsync(cancellationToken);

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

        var recentCourses = activeCourses
            .OrderByDescending(x => x.UpdatedAt)
            .ThenByDescending(x => x.Id)
            .Take(5)
            .ToList();

        return new AdminDashboardDto
        {
            CourseCount = activeCourses.Count,
            TopicCount = activeTopics.Count,
            ContentCount = learningContent.Count,
            StudentCount = students.Count,
            RecentCourses = recentCourses,
            ActiveCourses = activeCourses,
            ActiveTopics = activeTopics,
            LearningContent = learningContent,
            Students = students
        };
    }
}
