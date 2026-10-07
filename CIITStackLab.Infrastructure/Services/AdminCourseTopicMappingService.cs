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

        var assignedTopicIds = await _dbContext.CourseModules
            .AsNoTracking()
            .Where(x => x.CourseId == effectiveCourseId.Value && x.Flag == 0)
            .Select(x => x.TopicId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var assignedSet = assignedTopicIds.ToHashSet();

        var topicDtos = topics
            .Select(x => new AdminMappingTopicDto
            {
                Id = x.Id,
                Title = x.Title,
                IsAssigned = assignedSet.Contains(x.Id)
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

        var requestedTopicIds = topicIds
            .Distinct()
            .ToHashSet();

        var activeTopicIds = await _dbContext.Topics
            .AsNoTracking()
            .Where(x => x.Flag == 0)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        // Only currently active topics can participate in a mapping.
        requestedTopicIds.IntersectWith(activeTopicIds);

        var allMappings = await _dbContext.CourseModules
            .Where(x => x.CourseId == courseId)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);

        var currentActiveMappings = allMappings
            .Where(x => x.Flag == 0)
            .ToList();

        // Synchronize the selected checkboxes with the active mappings:
        // checked = keep/create/restore; unchecked = soft-delete.
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
            var activeMapping = currentActiveMappings
                .FirstOrDefault(x => x.TopicId == topicId);

            if (activeMapping is not null)
            {
                continue;
            }

            // Reuse an archived mapping for the same pair when possible.
            // This prevents multiple rows for the same Course + Topic pair
            // while allowing a previously removed topic to be added again.
            var archivedMapping = allMappings
                .Where(x => x.TopicId == topicId && x.Flag == 1)
                .OrderByDescending(x => x.Id)
                .FirstOrDefault();

            if (archivedMapping is not null)
            {
                archivedMapping.Flag = 0;
                archivedMapping.DeletedAt = null;
                archivedMapping.RestoredAt = DateTime.Now;
                archivedMapping.UpdatedAt = DateTime.Now;
            }
            else
            {
                _dbContext.CourseModules.Add(new Domain.Entities.CourseModule
                {
                    CourseId = courseId,
                    TopicId = topicId,
                    Flag = 0,
                    CreatedAt = DateTime.Now
                });
            }
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
