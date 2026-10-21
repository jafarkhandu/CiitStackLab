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

        var userIsActive = await _dbContext.Users
            .AsNoTracking()
            .AnyAsync(user => user.Id == userId && user.IsActive, cancellationToken);

        if (!userIsActive)
        {
            return false;
        }

        return await _dbContext.StudentCoursePurchases
            .AsNoTracking()
            .AnyAsync(
                purchase => purchase.UserId == userId
                    && _dbContext.Courses.Any(course =>
                        course.Id == purchase.CourseId && course.Flag == 0),
                cancellationToken);
    }

    public async Task<IReadOnlyList<int>> GetAccessibleCourseIdsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Array.Empty<int>();
        }

        var userIsActive = await _dbContext.Users
            .AsNoTracking()
            .AnyAsync(user => user.Id == userId && user.IsActive, cancellationToken);

        if (!userIsActive)
        {
            return Array.Empty<int>();
        }

        var publishedCourseIds = await _dbContext.Courses
            .AsNoTracking()
            .Where(course => course.Flag == 0)
            .Select(course => course.Id)
            .ToListAsync(cancellationToken);

        var purchasedCourseIds = await _dbContext.StudentCoursePurchases
            .AsNoTracking()
            .Where(purchase => purchase.UserId == userId)
            .Select(purchase => purchase.CourseId)
            .ToListAsync(cancellationToken);

        return purchasedCourseIds
            .Where(publishedCourseIds.Contains)
            .Distinct()
            .OrderBy(courseId => courseId)
            .ToArray();
    }

    public async Task<decimal> GetCoursePriceAsync(
        int courseId,
        CancellationToken cancellationToken = default)
    {
        if (courseId <= 0)
        {
            return 0m;
        }

        var courseDiscount = await _dbContext.Courses.AsNoTracking()
            .Where(course => course.Id == courseId && course.Flag == 0)
            .Select(course => new { course.DiscountMode, course.DiscountValue })
            .SingleOrDefaultAsync(cancellationToken);
        if (courseDiscount is null) return 0m;

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

        return await (
            from purchase in _dbContext.StudentCoursePurchases.AsNoTracking()
            join user in _dbContext.Users.AsNoTracking()
                on purchase.UserId equals user.Id
            join course in _dbContext.Courses.AsNoTracking()
                on purchase.CourseId equals course.Id
            where purchase.UserId == userId
                  && purchase.CourseId == courseId
                  && user.IsActive
                  && course.Flag == 0
            select purchase.Id)
            .AnyAsync(cancellationToken);
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

        return await HasPurchasedCourseAsync(userId, courseId, cancellationToken);
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
