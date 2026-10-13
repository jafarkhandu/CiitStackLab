using CIITStackLab.Application.DTOs;
using CIITStackLab.Application.Interfaces;
using CIITStackLab.Domain.Entities;
using CIITStackLab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CIITStackLab.Infrastructure.Services;

public sealed class CourseEnrollmentService : ICourseEnrollmentService
{
    private readonly ApplicationDbContext _dbContext;

    public CourseEnrollmentService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<CourseEnrollmentDto?> GetStudentEnrollmentAsync(
        string userId,
        int courseId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || courseId <= 0)
        {
            return null;
        }

        var enrollment = await _dbContext.CourseEnrollments
            .AsNoTracking()
            .Where(row => row.UserId == userId && row.CourseId == courseId)
            .Select(row => new CourseEnrollmentDto
            {
                Id = row.Id,
                CourseId = row.CourseId,
                Status = row.Status,
                PriceAtEnrollment = row.PriceAtEnrollment,
                RequestedAt = row.RequestedAt,
                ReviewedAt = row.ReviewedAt
            })
            .SingleOrDefaultAsync(cancellationToken);

        return enrollment;
    }

    public async Task<bool> HasActiveEnrollmentAsync(
        string userId,
        int courseId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || courseId <= 0)
        {
            return false;
        }

        return await (
            from enrollment in _dbContext.CourseEnrollments.AsNoTracking()
            join user in _dbContext.Users.AsNoTracking()
                on enrollment.UserId equals user.Id
            where enrollment.UserId == userId
                  && enrollment.CourseId == courseId
                  && enrollment.Status == CourseEnrollmentStatuses.Active
                  && user.IsActive
            select enrollment.Id)
            .AnyAsync(cancellationToken);
    }

    public async Task<decimal> GetCoursePriceAsync(
        int courseId,
        CancellationToken cancellationToken = default)
    {
        if (courseId <= 0)
        {
            return 0m;
        }

        var activeTopicPrices = await (
            from course in _dbContext.Courses.AsNoTracking()
            join courseTopic in _dbContext.CourseModules.AsNoTracking()
                on course.Id equals courseTopic.CourseId
            join topic in _dbContext.Topics.AsNoTracking()
                on courseTopic.TopicId equals topic.Id
            where course.Id == courseId
                  && course.Flag == 0
                  && courseTopic.Flag == 0
                  && topic.Flag == 0
            select new
            {
                TopicId = topic.Id,
                Price = topic.Price
            })
            .Distinct()
            .ToListAsync(cancellationToken);

        return activeTopicPrices.Sum(row => Math.Max(0m, row.Price));
    }

    public async Task<bool> CanAccessCourseAsync(
        string? userId,
        int courseId,
        CancellationToken cancellationToken = default)
    {
        if (courseId <= 0)
        {
            return false;
        }

        var isPublished = await _dbContext.Courses
            .AsNoTracking()
            .AnyAsync(course => course.Id == courseId && course.Flag == 0, cancellationToken);

        if (!isPublished)
        {
            return false;
        }

        var price = await GetCoursePriceAsync(courseId, cancellationToken);

        if (price <= 0m)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        return await HasActiveEnrollmentAsync(userId, courseId, cancellationToken);
    }

    public async Task<bool> CanAccessTopicAsync(
        string? userId,
        int topicId,
        CancellationToken cancellationToken = default)
    {
        if (topicId <= 0)
        {
            return false;
        }

        var isActiveTopic = await _dbContext.Topics
            .AsNoTracking()
            .AnyAsync(topic => topic.Id == topicId && topic.Flag == 0, cancellationToken);

        if (!isActiveTopic)
        {
            return false;
        }

        var courseIds = await (
            from course in _dbContext.Courses.AsNoTracking()
            join courseTopic in _dbContext.CourseModules.AsNoTracking()
                on course.Id equals courseTopic.CourseId
            where courseTopic.TopicId == topicId
                  && course.Flag == 0
                  && courseTopic.Flag == 0
            select course.Id)
            .Distinct()
            .ToListAsync(cancellationToken);

        foreach (var courseId in courseIds)
        {
            if (await CanAccessCourseAsync(userId, courseId, cancellationToken))
            {
                return true;
            }
        }

        return false;
    }


}
