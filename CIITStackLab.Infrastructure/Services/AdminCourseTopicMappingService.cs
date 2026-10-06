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

        if (requestedTopicIds.Count == 0)
        {
            return (false, "Select at least one new topic before saving.");
        }

        var activeTopicIds = await _dbContext.Topics
            .AsNoTracking()
            .Where(x => x.Flag == 0)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        requestedTopicIds.IntersectWith(activeTopicIds);

        if (requestedTopicIds.Count == 0)
        {
            return (false, "The selected topics are no longer active.");
        }

        // A course-topic pair may exist only once. The existing ERP database
        // enforces a unique constraint on (course_id, topic_id), so previously
        // mapped pairs are never inserted again.
        var existingTopicIds = await _dbContext.CourseModules
            .AsNoTracking()
            .Where(x => x.CourseId == courseId && requestedTopicIds.Contains(x.TopicId))
            .Select(x => x.TopicId)
            .Distinct()
            .ToListAsync(cancellationToken);

        requestedTopicIds.ExceptWith(existingTopicIds);

        if (requestedTopicIds.Count == 0)
        {
            return (false, "All selected topics are already mapped to this course.");
        }

        foreach (var topicId in requestedTopicIds)
        {
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
            return (false, "The course-topic mapping could not be saved. A duplicate mapping may already exist.");
        }
    }
}
