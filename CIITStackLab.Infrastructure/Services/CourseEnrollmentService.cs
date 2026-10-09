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

    public async Task<CourseEnrollmentRequestResultDto> RequestEnrollmentAsync(
        string userId,
        int courseId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || courseId <= 0)
        {
            return CourseEnrollmentRequestResultDto.Failure(
                "A valid student account and course are required.");
        }

        var studentExists = await _dbContext.Users
            .AsNoTracking()
            .AnyAsync(user => user.Id == userId && user.IsActive, cancellationToken);

        if (!studentExists)
        {
            return CourseEnrollmentRequestResultDto.Failure(
                "Your student account is not active.");
        }

        var course = await _dbContext.Courses
            .AsNoTracking()
            .SingleOrDefaultAsync(
                row => row.Id == courseId && row.Flag == 0,
                cancellationToken);

        if (course is null)
        {
            return CourseEnrollmentRequestResultDto.Failure(
                "The selected course is not available.");
        }

        var coursePrice = await GetCoursePriceAsync(courseId, cancellationToken);
        var existing = await _dbContext.CourseEnrollments
            .SingleOrDefaultAsync(
                row => row.UserId == userId && row.CourseId == courseId,
                cancellationToken);

        if (existing?.Status == CourseEnrollmentStatuses.Active)
        {
            return new CourseEnrollmentRequestResultDto
            {
                Succeeded = true,
                HasAccess = true,
                IsFreeCourse = coursePrice <= 0m,
                Status = existing.Status,
                Message = "You already have access to this course."
            };
        }

        if (coursePrice > 0m
            && existing?.Status == CourseEnrollmentStatuses.PendingApproval)
        {
            return new CourseEnrollmentRequestResultDto
            {
                Succeeded = true,
                HasAccess = false,
                IsFreeCourse = false,
                Status = existing.Status,
                Message = "Your enrollment request is awaiting administrator approval."
            };
        }

        var now = DateTime.UtcNow;
        var newStatus = coursePrice <= 0m
            ? CourseEnrollmentStatuses.Active
            : CourseEnrollmentStatuses.PendingApproval;

        if (existing is null)
        {
            existing = new CourseEnrollment
            {
                UserId = userId,
                CourseId = courseId,
                Status = newStatus,
                PriceAtEnrollment = coursePrice,
                RequestedAt = now
            };

            _dbContext.CourseEnrollments.Add(existing);
        }
        else
        {
            existing.Status = newStatus;
            existing.PriceAtEnrollment = coursePrice;
            existing.RequestedAt = now;
            existing.ReviewedAt = null;
            existing.ReviewedByUserId = null;
        }

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            _dbContext.Entry(existing).State = EntityState.Detached;

            var latestEnrollment = await GetStudentEnrollmentAsync(
                userId,
                courseId,
                cancellationToken);

            if (latestEnrollment is not null)
            {
                return new CourseEnrollmentRequestResultDto
                {
                    Succeeded = latestEnrollment.Status == CourseEnrollmentStatuses.Active
                        || latestEnrollment.Status == CourseEnrollmentStatuses.PendingApproval,
                    HasAccess = latestEnrollment.HasAccess,
                    IsFreeCourse = latestEnrollment.PriceAtEnrollment <= 0m,
                    Status = latestEnrollment.Status,
                    Message = latestEnrollment.HasAccess
                        ? "You already have access to this course."
                        : "Your enrollment request is awaiting administrator approval."
                };
            }

            return CourseEnrollmentRequestResultDto.Failure(
                "Your enrollment could not be saved. Please try again.");
        }

        return new CourseEnrollmentRequestResultDto
        {
            Succeeded = true,
            HasAccess = newStatus == CourseEnrollmentStatuses.Active,
            IsFreeCourse = coursePrice <= 0m,
            Status = newStatus,
            Message = coursePrice <= 0m
                ? "Enrollment successful. You can start learning now."
                : "Enrollment request submitted. Access will be enabled after administrator approval."
        };
    }

    public async Task<IReadOnlyList<PendingCourseEnrollmentDto>> GetPendingAsync(
        CancellationToken cancellationToken = default)
    {
        return await (
            from enrollment in _dbContext.CourseEnrollments.AsNoTracking()
            join course in _dbContext.Courses.AsNoTracking()
                on enrollment.CourseId equals course.Id
            join user in _dbContext.Users.AsNoTracking()
                on enrollment.UserId equals user.Id
            where enrollment.Status == CourseEnrollmentStatuses.PendingApproval
                  && course.Flag == 0
            orderby enrollment.RequestedAt
            select new PendingCourseEnrollmentDto
            {
                Id = enrollment.Id,
                CourseId = enrollment.CourseId,
                CourseTitle = course.Title,
                UserId = enrollment.UserId,
                StudentUserName = user.UserName ?? string.Empty,
                StudentEmail = user.Email ?? string.Empty,
                PriceAtEnrollment = enrollment.PriceAtEnrollment,
                RequestedAt = enrollment.RequestedAt,
                Status = enrollment.Status
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ReviewAsync(
        int enrollmentId,
        bool approve,
        string reviewerUserId,
        CancellationToken cancellationToken = default)
    {
        if (enrollmentId <= 0 || string.IsNullOrWhiteSpace(reviewerUserId))
        {
            return false;
        }

        var enrollment = await _dbContext.CourseEnrollments
            .SingleOrDefaultAsync(
                row => row.Id == enrollmentId
                    && row.Status == CourseEnrollmentStatuses.PendingApproval,
                cancellationToken);

        if (enrollment is null)
        {
            return false;
        }

        var courseIsActive = await _dbContext.Courses
            .AsNoTracking()
            .AnyAsync(
                course => course.Id == enrollment.CourseId && course.Flag == 0,
                cancellationToken);

        if (!courseIsActive)
        {
            return false;
        }

        enrollment.Status = approve
            ? CourseEnrollmentStatuses.Active
            : CourseEnrollmentStatuses.Rejected;
        enrollment.ReviewedAt = DateTime.UtcNow;
        enrollment.ReviewedByUserId = reviewerUserId;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
