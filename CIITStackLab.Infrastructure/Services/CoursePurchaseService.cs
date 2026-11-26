using CIITStackLab.Application.DTOs;
using CIITStackLab.Domain.Entities;
using CIITStackLab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CIITStackLab.Infrastructure.Services;

public sealed class CoursePurchaseService
{
    private readonly ApplicationDbContext _dbContext;

    public CoursePurchaseService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<bool> CompleteAsync(
        string userId,
        int courseId,
        CancellationToken cancellationToken = default)
    {
        return CompleteInternalAsync(userId, courseId, null, cancellationToken);
    }

    public Task<bool> CompleteWithPriceAsync(
        string userId,
        int courseId,
        decimal purchasePrice,
        CancellationToken cancellationToken = default)
    {
        return CompleteInternalAsync(userId, courseId, purchasePrice, cancellationToken);
    }

    private async Task<bool> CompleteInternalAsync(
        string userId,
        int courseId,
        decimal? purchasePrice,
        CancellationToken cancellationToken)
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

        var currentCoursePrice = await GetCoursePriceAsync(courseId, cancellationToken);

        if (currentCoursePrice <= 0m)
        {
            return true;
        }

        var existingPurchase = await _dbContext.StudentCoursePurchases
            .AsNoTracking()
            .AnyAsync(
                purchase => purchase.UserId == userId && purchase.CourseId == courseId,
                cancellationToken);

        if (existingPurchase)
        {
            return true;
        }

        var finalPurchasePrice = purchasePrice ?? currentCoursePrice;
        if (finalPurchasePrice <= 0m)
        {
            return false;
        }

        var purchase = new StudentCoursePurchase
        {
            UserId = userId,
            CourseId = courseId,
            PurchasePrice = finalPurchasePrice,
            PurchasedAt = DateTime.UtcNow
        };

        _dbContext.StudentCoursePurchases.Add(purchase);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            _dbContext.Entry(purchase).State = EntityState.Detached;

            return await _dbContext.StudentCoursePurchases
                .AsNoTracking()
                .AnyAsync(
                    purchase => purchase.UserId == userId && purchase.CourseId == courseId,
                    cancellationToken);
        }
    }

    public async Task<bool> HasAnyActivePurchaseAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        return await QueryPurchasedCourses(_dbContext, userId).AnyAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<int>> GetAccessibleCourseIdsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Array.Empty<int>();
        }

        return await QueryPurchasedCourses(_dbContext, userId)
            .Select(course => course.Id)
            .OrderBy(courseId => courseId)
            .ToArrayAsync(cancellationToken);
    }

    internal static IQueryable<Course> QueryPurchasedCourses(ApplicationDbContext dbContext, string userId)
    {
        return dbContext.Courses
            .AsNoTracking()
            .Where(course => course.Flag == 0
                && dbContext.Users.Any(user => user.Id == userId && user.IsActive)
                && dbContext.StudentCoursePurchases.Any(purchase =>
                    purchase.UserId == userId && purchase.CourseId == course.Id));
    }

    public async Task<IReadOnlyList<CourseDto>> GetCourseSwitcherItemsAsync(
        string userId,
        int? currentCourseId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Array.Empty<CourseDto>();
        }

        // Return the student's published purchases and the already-authorized current
        // course (for demo/current-course access) in one lightweight query. The layout
        // only displays course IDs and titles, so duration and presentation data are not needed.
        return await _dbContext.Courses
            .AsNoTracking()
            .Where(course => course.Flag == 0
                && ((currentCourseId.HasValue && course.Id == currentCourseId.Value)
                    || (_dbContext.Users.Any(user => user.Id == userId && user.IsActive)
                        && _dbContext.StudentCoursePurchases.Any(purchase =>
                            purchase.UserId == userId && purchase.CourseId == course.Id))))
            .OrderBy(course => course.Title)
            .Select(course => new CourseDto
            {
                Id = course.Id,
                Title = course.Title
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<decimal> GetCoursePriceAsync(
        int courseId,
        CancellationToken cancellationToken = default)
        => await GetPublishedCoursePriceAsync(courseId, cancellationToken) ?? 0m;

    private async Task<decimal?> GetPublishedCoursePriceAsync(
        int courseId,
        CancellationToken cancellationToken)
    {
        if (courseId <= 0)
        {
            return null;
        }

        var courseDiscount = await _dbContext.Courses.AsNoTracking()
            .Where(course => course.Id == courseId && course.Flag == 0)
            .Select(course => new { course.DiscountMode, course.DiscountValue })
            .SingleOrDefaultAsync(cancellationToken);
        if (courseDiscount is null) return null;

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

        var originalPrice = activeTopicPrices.Sum(row => Math.Max(0m, row.Price));
        if (originalPrice <= 0m || !courseDiscount.DiscountValue.HasValue) return originalPrice;
        var value = courseDiscount.DiscountValue.Value;
        var discountedPrice = courseDiscount.DiscountMode switch
        {
            "percentage" when value >= 0m && value <= 100m => originalPrice * (1m - value / 100m),
            "direct" when value >= 0m && value <= originalPrice => value,
            _ => originalPrice
        };
        return Math.Round(Math.Clamp(discountedPrice, 0m, originalPrice), 2, MidpointRounding.AwayFromZero);
    }

    public async Task<bool> HasPurchasedCourseAsync(
        string userId,
        int courseId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || courseId <= 0)
        {
            return false;
        }

        return await QueryPurchasedCourses(_dbContext, userId)
            .AnyAsync(course => course.Id == courseId, cancellationToken);
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

        if (!string.IsNullOrWhiteSpace(userId)
            && await HasPurchasedCourseAsync(userId, courseId, cancellationToken))
        {
            return true;
        }

        return await GetPublishedCoursePriceAsync(courseId, cancellationToken) is <= 0m;
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
