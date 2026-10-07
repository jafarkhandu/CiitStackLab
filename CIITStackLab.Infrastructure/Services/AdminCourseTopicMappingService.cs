using CIITStackLab.Application.DTOs;
using CIITStackLab.Application.Interfaces;
using CIITStackLab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CIITStackLab.Infrastructure.Services;

public sealed class AdminCourseTopicMappingService : IAdminCourseTopicMappingService
{
    private readonly ApplicationDbContext _dbContext;

    public AdminCourseTopicMappingService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AdminCourseTopicMappingPageDto> GetPageAsync(
        int? selectedCourseId,
        CancellationToken cancellationToken = default)
    {
        var courses = await _dbContext.Courses
            .AsNoTracking()
            .Where(x => x.Flag == 0)
            .OrderBy(x => x.Title)
            .Select(x => new AdminMappingCourseDto
            {
                Id = x.Id,
                Title = x.Title
            })
            .ToListAsync(cancellationToken);

        var effectiveCourseId = selectedCourseId.HasValue &&
                                courses.Any(x => x.Id == selectedCourseId.Value)
            ? selectedCourseId.Value
            : courses.FirstOrDefault()?.Id;

        if (!effectiveCourseId.HasValue)
        {
            return new AdminCourseTopicMappingPageDto
            {
                Courses = courses
            };
        }

        var selectedCourse = courses.First(x => x.Id == effectiveCourseId.Value);

        var topics = await _dbContext.Topics
            .AsNoTracking()
            .Where(x => x.Flag == 0)
            .OrderBy(x => x.Title)
            .Select(x => new
            {
                x.Id,
                x.Title
            })
            .ToListAsync(cancellationToken);

        var mappings = await _dbContext.CourseModules
            .AsNoTracking()
            .Where(x => x.CourseId == effectiveCourseId.Value)
            .Select(x => new
            {
                x.TopicId,
                x.Flag
            })
            .ToListAsync(cancellationToken);

        var assignedTopicIds = mappings
            .Where(x => x.Flag == 0)
            .Select(x => x.TopicId)
            .Distinct()
            .ToHashSet();

        var historicalTopicIds = mappings
            .Select(x => x.TopicId)
            .Distinct()
            .ToHashSet();

        var topicDtos = topics
            .Select(x => new AdminMappingTopicDto
            {
                Id = x.Id,
                Title = x.Title,
                IsAssigned = assignedTopicIds.Contains(x.Id),
                WasEverMapped = historicalTopicIds.Contains(x.Id)
            })
            .ToList();

        return new AdminCourseTopicMappingPageDto
        {
            Courses = courses,
            SelectedCourseId = effectiveCourseId.Value,
            SelectedCourseName = selectedCourse.Title,
            Topics = topicDtos,
            AssignedTopicCount = assignedTopicIds.Count
        };
    }

    public async Task<(bool Succeeded, string? Error)> SaveAsync(
        int courseId,
        IReadOnlyCollection<int> topicIds,
        CancellationToken cancellationToken = default)
    {
        var courseExists = await _dbContext.Courses
            .AsNoTracking()
            .AnyAsync(x => x.Id == courseId && x.Flag == 0, cancellationToken);

        if (!courseExists)
        {
            return (false, "The selected course is no longer active.");
        }

        var activeTopicIds = await _dbContext.Topics
            .AsNoTracking()
            .Where(x => x.Flag == 0)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var requestedTopicIds = topicIds
            .Intersect(activeTopicIds)
            .Distinct()
            .ToHashSet();

        var allMappings = await _dbContext.CourseModules
            .Where(x => x.CourseId == courseId)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);

        var currentActiveMappings = allMappings
            .Where(x => x.Flag == 0)
            .ToList();

        foreach (var activeMapping in currentActiveMappings)
        {
            if (!requestedTopicIds.Contains(activeMapping.TopicId))
            {
                activeMapping.Flag = 1;
                activeMapping.DeletedAt = DateTime.Now;
                activeMapping.UpdatedAt = DateTime.Now;
            }
        }

        foreach (var topicId in requestedTopicIds)
        {
            if (currentActiveMappings.Any(x => x.TopicId == topicId))
            {
                continue;
            }

            var removedMapping = allMappings.FirstOrDefault(x =>
                x.TopicId == topicId && x.Flag != 0);

            if (removedMapping != null)
            {
                removedMapping.Flag = 0;
                removedMapping.DeletedAt = null;
                removedMapping.RestoredAt = DateTime.Now;
                removedMapping.UpdatedAt = DateTime.Now;
                continue;
            }

            _dbContext.CourseModules.Add(new Domain.Entities.CourseModule
            {
                CourseId = courseId,
                TopicId = topicId,
                Flag = 0,
                CreatedAt = DateTime.Now
            });
        }

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return (true, null);
        }
        catch (DbUpdateException)
        {
            return (false, "The course-topic mapping could not be saved. Please try again.");
        }
    }
}
