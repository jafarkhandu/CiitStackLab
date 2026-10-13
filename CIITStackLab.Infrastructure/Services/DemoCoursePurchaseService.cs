using CIITStackLab.Domain.Entities;
using CIITStackLab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CIITStackLab.Infrastructure.Services;

public sealed class DemoCoursePurchaseService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly CourseEnrollmentService _courseEnrollmentService;

    public DemoCoursePurchaseService(
        ApplicationDbContext dbContext,
        CourseEnrollmentService courseEnrollmentService)
    {
        _dbContext = dbContext;
        _courseEnrollmentService = courseEnrollmentService;
    }

    public async Task<bool> CompleteAsync(
        string userId,
        int courseId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || courseId <= 0)
        {
            return false;
        }

        var userIsActive = await _dbContext.Users
            .AsNoTracking()
            .AnyAsync(user => user.Id == userId && user.IsActive, cancellationToken);

        if (!userIsActive)
        {
            return false;
        }

        var courseExists = await _dbContext.Courses
            .AsNoTracking()
            .AnyAsync(course => course.Id == courseId && course.Flag == 0, cancellationToken);

        if (!courseExists)
        {
            return false;
        }

        var price = await _courseEnrollmentService.GetCoursePriceAsync(
            courseId,
            cancellationToken);

        if (price <= 0m)
        {
            return true;
        }

        var now = DateTime.UtcNow;
        var existing = await _dbContext.CourseEnrollments
            .SingleOrDefaultAsync(
                row => row.UserId == userId && row.CourseId == courseId,
                cancellationToken);

        if (existing is not null && existing.Status == CourseEnrollmentStatuses.Active)
        {
            return true;
        }

        if (existing is null)
        {
            _dbContext.CourseEnrollments.Add(new CourseEnrollment
            {
                UserId = userId,
                CourseId = courseId,
                Status = CourseEnrollmentStatuses.Active,
                PriceAtEnrollment = price,
                RequestedAt = now
            });
        }
        else
        {
            existing.Status = CourseEnrollmentStatuses.Active;
            existing.PriceAtEnrollment = price;
            existing.RequestedAt = now;
            existing.ReviewedAt = null;
            existing.ReviewedByUserId = null;
        }

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            var activePurchaseExists = await _dbContext.CourseEnrollments
                .AsNoTracking()
                .AnyAsync(
                    row => row.UserId == userId
                        && row.CourseId == courseId
                        && row.Status == CourseEnrollmentStatuses.Active,
                    cancellationToken);

            return activePurchaseExists;
        }
    }
}
