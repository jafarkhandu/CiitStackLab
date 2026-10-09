using CIITStackLab.Application.DTOs;
using CIITStackLab.Application.Interfaces;
using CIITStackLab.Domain.Entities;
using CIITStackLab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CIITStackLab.Infrastructure.Services;

public sealed class StudentProgressService : IStudentProgressService
{
    private readonly ApplicationDbContext _dbContext;

    public StudentProgressService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<int?> GetResumeContentIdAsync(
        string userId,
        int courseId,
        CancellationToken cancellationToken = default)
    {
        var progress = await GetProgressAsync(
            userId,
            courseId,
            cancellationToken);

        return progress.LastAccessedContentId;
    }

    public async Task<CourseLearningProgressDto> GetProgressAsync(
        string userId,
        int courseId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || courseId <= 0)
        {
            return EmptyProgress();
        }

        var activeContentIds = await GetActiveContentIdsAsync(
            courseId,
            cancellationToken);

        if (activeContentIds.Count == 0)
        {
            return EmptyProgress();
        }

        var progressRows = await _dbContext.StudentLessonProgress
            .AsNoTracking()
            .Where(progress =>
                progress.UserId == userId
                && progress.CourseId == courseId
                && activeContentIds.Contains(progress.ContentId))
            .OrderByDescending(progress => progress.LastAccessedAt)
            .ThenByDescending(progress => progress.Id)
            .ToListAsync(cancellationToken);

        var completedContentIds = progressRows
            .Where(progress => progress.IsCompleted)
            .Select(progress => progress.ContentId)
            .Distinct()
            .ToList();

        var totalLessons = activeContentIds.Count;
        var completedLessons = completedContentIds.Count;
        var percentage = totalLessons == 0
            ? 0
            : (int)Math.Round(
                completedLessons * 100d / totalLessons,
                MidpointRounding.AwayFromZero);

        return new CourseLearningProgressDto
        {
            TotalLessons = totalLessons,
            CompletedLessons = completedLessons,
            Percentage = percentage,
            LastAccessedContentId = progressRows.FirstOrDefault()?.ContentId,
            CompletedContentIds = completedContentIds
        };
    }

    public async Task<bool> RecordAccessAsync(
        string userId,
        int courseId,
        int contentId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId)
            || courseId <= 0
            || contentId <= 0)
        {
            return false;
        }

        var isAccessible = await IsActiveCourseContentAsync(
            courseId,
            contentId,
            cancellationToken);

        if (!isAccessible)
        {
            return false;
        }

        var progress = await FindOrCreateProgressAsync(
            userId,
            courseId,
            contentId,
            cancellationToken);

        progress.LastAccessedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> MarkCompletedAsync(
        string userId,
        int courseId,
        int contentId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId)
            || courseId <= 0
            || contentId <= 0)
        {
            return false;
        }

        var isAccessible = await IsActiveCourseContentAsync(
            courseId,
            contentId,
            cancellationToken);

        if (!isAccessible)
        {
            return false;
        }

        var progress = await FindOrCreateProgressAsync(
            userId,
            courseId,
            contentId,
            cancellationToken);

        var completedAt = DateTime.UtcNow;

        progress.LastAccessedAt = completedAt;

        if (!progress.IsCompleted)
        {
            progress.IsCompleted = true;
            progress.CompletedAt = completedAt;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    private async Task<StudentLessonProgress> FindOrCreateProgressAsync(
        string userId,
        int courseId,
        int contentId,
        CancellationToken cancellationToken)
    {
        var progress = await _dbContext.StudentLessonProgress
            .SingleOrDefaultAsync(
                row => row.UserId == userId
                    && row.CourseId == courseId
                    && row.ContentId == contentId,
                cancellationToken);

        if (progress is not null)
        {
            return progress;
        }

        progress = new StudentLessonProgress
        {
            UserId = userId,
            CourseId = courseId,
            ContentId = contentId,
            IsCompleted = false,
            LastAccessedAt = DateTime.UtcNow
        };

        _dbContext.StudentLessonProgress.Add(progress);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return progress;
        }
        catch (DbUpdateException)
        {
            _dbContext.Entry(progress).State = EntityState.Detached;

            var existingProgress = await _dbContext.StudentLessonProgress
                .SingleOrDefaultAsync(
                    row => row.UserId == userId
                        && row.CourseId == courseId
                        && row.ContentId == contentId,
                    cancellationToken);

            if (existingProgress is null)
            {
                throw;
            }

            return existingProgress;
        }
    }

    private async Task<List<int>> GetActiveContentIdsAsync(
        int courseId,
        CancellationToken cancellationToken)
    {
        return await (
            from course in _dbContext.Courses.AsNoTracking()
            join courseTopic in _dbContext.CourseModules.AsNoTracking()
                on course.Id equals courseTopic.CourseId
            join topic in _dbContext.Topics.AsNoTracking()
                on courseTopic.TopicId equals topic.Id
            join lesson in _dbContext.Lessons.AsNoTracking()
                on topic.Id equals lesson.TopicId
            where course.Id == courseId
                  && course.Flag == 0
                  && courseTopic.Flag == 0
                  && topic.Flag == 0
                  && lesson.Flag == 0
            select lesson.Id)
            .Distinct()
            .OrderBy(contentId => contentId)
            .ToListAsync(cancellationToken);
    }

    private async Task<bool> IsActiveCourseContentAsync(
        int courseId,
        int contentId,
        CancellationToken cancellationToken)
    {
        return await (
            from course in _dbContext.Courses.AsNoTracking()
            join courseTopic in _dbContext.CourseModules.AsNoTracking()
                on course.Id equals courseTopic.CourseId
            join topic in _dbContext.Topics.AsNoTracking()
                on courseTopic.TopicId equals topic.Id
            join lesson in _dbContext.Lessons.AsNoTracking()
                on topic.Id equals lesson.TopicId
            where course.Id == courseId
                  && course.Flag == 0
                  && courseTopic.Flag == 0
                  && topic.Flag == 0
                  && lesson.Flag == 0
                  && lesson.Id == contentId
            select lesson.Id)
            .AnyAsync(cancellationToken);
    }

    private static CourseLearningProgressDto EmptyProgress()
    {
        return new CourseLearningProgressDto
        {
            TotalLessons = 0,
            CompletedLessons = 0,
            Percentage = 0,
            LastAccessedContentId = null,
            CompletedContentIds = Array.Empty<int>()
        };
    }
}
