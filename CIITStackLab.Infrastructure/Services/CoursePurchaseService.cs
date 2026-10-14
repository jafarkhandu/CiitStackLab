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

        var price = await GetCoursePriceAsync(courseId, cancellationToken);

        if (price <= 0m)
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

        var purchase = new StudentCoursePurchase
        {
            UserId = userId,
            CourseId = courseId,
            PurchasePrice = price,
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

        return await _dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == userId && user.IsActive)
            .SelectMany(user => _dbContext.StudentCoursePurchases
                .Where(purchase => purchase.UserId == user.Id)
                .Join(
                    _dbContext.Courses.Where(course => course.Flag == 0),
                    purchase => purchase.CourseId,
                    course => course.Id,
                    (purchase, course) => purchase.Id))
            .AnyAsync(cancellationToken);
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

        var courseTopicPrices = await (
            from course in _dbContext.Courses.AsNoTracking()
            join courseTopic in _dbContext.CourseModules.AsNoTracking()
                on course.Id equals courseTopic.CourseId
            join topic in _dbContext.Topics.AsNoTracking()
                on courseTopic.TopicId equals topic.Id
            where course.Flag == 0
                  && courseTopic.Flag == 0
                  && topic.Flag == 0
            select new
            {
                CourseId = course.Id,
                TopicId = topic.Id,
                Price = topic.Price
            })
            .Distinct()
            .ToListAsync(cancellationToken);

        var priceByCourseId = courseTopicPrices
            .GroupBy(row => row.CourseId)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(row => Math.Max(0m, row.Price)));

        var accessibleCourseIds = purchasedCourseIds.ToHashSet();

        foreach (var courseId in publishedCourseIds)
        {
            if (!priceByCourseId.TryGetValue(courseId, out var price) || price <= 0m)
            {
                accessibleCourseIds.Add(courseId);
            }
        }

        return accessibleCourseIds
            .Where(publishedCourseIds.Contains)
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
