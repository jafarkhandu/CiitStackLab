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

        var activeTopicIds = await _dbContext.Topics
            .AsNoTracking()
            .Where(x => x.Flag == 0)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var requestedTopicIds = topicIds
            .Where(activeTopicIds.Contains)
            .Distinct()
            .ToHashSet();

        var mappings = await _dbContext.CourseModules
            .Where(x => x.CourseId == courseId)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            foreach (var topicId in activeTopicIds)
            {
                var topicMappings = mappings
                    .Where(x => x.TopicId == topicId)
                    .ToList();

                var activeMapping = topicMappings.FirstOrDefault(x => x.Flag == 0);

                if (requestedTopicIds.Contains(topicId))
                {
                    if (activeMapping is not null)
                    {
                        continue;
                    }

                    var archivedMapping = topicMappings
                        .Where(x => x.Flag == 1)
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

                    continue;
                }

                if (activeMapping is not null)
                {
                    activeMapping.Flag = 1;
                    activeMapping.DeletedAt = DateTime.Now;
                    activeMapping.UpdatedAt = DateTime.Now;
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return (true, null);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return (false, "The course-topic mapping could not be saved.");
        }
    }
}
