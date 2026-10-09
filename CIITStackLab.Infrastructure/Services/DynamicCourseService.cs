using CIITStackLab.Application.DTOs;
using CIITStackLab.Application.Interfaces;
using CIITStackLab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CIITStackLab.Infrastructure.Services;

public sealed class DynamicCourseService : ICourseService
{
    private readonly CourseService _inner;
    private readonly ApplicationDbContext _dbContext;

    public DynamicCourseService(CourseService inner, ApplicationDbContext dbContext)
    {
        _inner = inner;
        _dbContext = dbContext;
    }

    public Task<IReadOnlyList<CourseDto>> GetPublishedAsync(CancellationToken cancellationToken = default)
        => _inner.GetPublishedAsync(cancellationToken);

    public Task<CourseDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => _inner.GetByIdAsync(id, cancellationToken);

    public async Task<CourseDetailsDto?> GetDetailsAsync(int id, CancellationToken cancellationToken = default)
    {
        var details = await _inner.GetDetailsAsync(id, cancellationToken);
        if (details is null)
        {
            return null;
        }

        return new CourseDetailsDto
        {
            Id = details.Id,
            Title = details.Title,
            ShortDescription = details.ShortDescription,
            Description = details.Description,
            ImageUrl = details.ImageUrl,
            Category = details.Category,
            Level = details.Level,
            DurationHours = details.DurationHours,
            Topics = details.Topics.Select(topic => new CourseTopicDto
            {
                Id = topic.Id,
                Title = topic.Title,
                HasNotes = topic.HasNotes,
                Contents = topic.Contents.Select(content => new CourseContentDto
                {
                    Id = content.Id,
                    Title = content.Title,
                    Slides = content.Slides,
                    VideoName = content.VideoName,
                    Questions = content.Questions
                }).ToList()
            }).ToList()
        };
    }

    public async Task<CourseLearningDto?> GetLearningAsync(
        int id,
        int? contentId = null,
        CancellationToken cancellationToken = default)
    {
        var course = await _inner.GetByIdAsync(id, cancellationToken);
        if (course is null)
        {
            return null;
        }

        var topicRows = await (
            from courseTopic in _dbContext.CourseModules.AsNoTracking()
            join topic in _dbContext.Topics.AsNoTracking()
                on courseTopic.TopicId equals topic.Id
            where courseTopic.CourseId == id
                  && courseTopic.Flag == 0
                  && topic.Flag == 0
            select new
            {
                TopicId = topic.Id,
                TopicTitle = topic.Title,
                TopicPrice = topic.Price,
                TopicDurationMinutes = topic.DurationMinutes
            })
            .ToListAsync(cancellationToken);

        var topicData = topicRows
            .GroupBy(x => new
            {
                x.TopicId,
                x.TopicTitle,
                x.TopicPrice,
                x.TopicDurationMinutes
            })
            .Select(group => group.Key)
            .OrderBy(x => x.TopicId)
            .ToList();

        var topicIds = topicData.Select(x => x.TopicId).ToList();

        var contentRows = topicIds.Count == 0
            ? []
            : await _dbContext.Lessons
                .AsNoTracking()
                .Where(x => x.TopicId.HasValue
                            && topicIds.Contains(x.TopicId.Value)
                            && x.Flag == 0)
                .OrderBy(x => x.TopicId)
                .ThenBy(x => x.Id)
                .Select(x => new
                {
                    x.Id,
                    x.TopicId,
                    x.NoteId,
                    x.Title,
                    x.Slides,
                    x.VideoName
                })
                .ToListAsync(cancellationToken);

        var chapterRows = topicIds.Count == 0
            ? []
            : await _dbContext.TrainingNotes
                .AsNoTracking()
                .Where(note => topicIds.Contains(note.TopicId) && note.Flag == 0)
                .OrderBy(note => note.TopicId)
                .ThenBy(note => note.SortOrder)
                .ThenBy(note => note.Id)
                .Select(note => new
                {
                    note.Id,
                    note.TopicId,
                    note.ChapterId,
                    note.Title,
                    note.SortOrder
                })
                .ToListAsync(cancellationToken);

        var contentIds = contentRows.Select(x => x.Id).Distinct().ToList();

        var questions = contentIds.Count == 0
            ? []
            : await _dbContext.ContentQuestions
                .AsNoTracking()
                .Where(x => x.ContentId.HasValue
                            && contentIds.Contains(x.ContentId.Value)
                            && x.Flag == 0)
                .OrderBy(x => x.Id)
                .ToListAsync(cancellationToken);

        var questionsByContentId = questions
            .GroupBy(x => x.ContentId!.Value)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ContentQuestionDto>)group.Select(x => new ContentQuestionDto
                {
                    Id = x.Id,
                    Question = x.Question ?? string.Empty,
                    Options = new[] { x.Option1, x.Option2, x.Option3, x.Option4 },
                    CorrectOptionNumber = x.CorrectOptionNumber
                }).ToList());

        var topics = topicData
            .Select(topic => new CourseLearningTopicDto
            {
                Id = topic.TopicId,
                Title = topic.TopicTitle,
                Price = topic.TopicPrice,
                DurationMinutes = topic.TopicDurationMinutes,
                Contents = contentRows
                    .Where(content => content.TopicId == topic.TopicId)
                    .Select(content => new CourseLearningContentDto
                    {
                        Id = content.Id,
                        Title = content.Title ?? string.Empty,
                        Slides = content.Slides,
                        VideoName = content.VideoName,
                        Questions = questionsByContentId.TryGetValue(content.Id, out var contentQuestions)
                            ? contentQuestions
                            : Array.Empty<ContentQuestionDto>()
                    })
                    .ToList(),
                Chapters = chapterRows
                    .Where(chapter => chapter.TopicId == topic.TopicId)
                    .Select(chapter => new CourseLearningChapterDto
                    {
                        Id = chapter.Id,
                        ChapterId = chapter.ChapterId,
                        Title = chapter.Title ?? string.Empty,
                        SortOrder = chapter.SortOrder,
                        Contents = contentRows
                            .Where(content => content.TopicId == topic.TopicId
                                              && content.NoteId == chapter.Id)
                            .Select(content => new CourseLearningContentDto
                            {
                                Id = content.Id,
                                Title = content.Title ?? string.Empty,
                                Slides = content.Slides,
                                VideoName = content.VideoName,
                                Questions = questionsByContentId.TryGetValue(content.Id, out var chapterQuestions)
                                    ? chapterQuestions
                                    : Array.Empty<ContentQuestionDto>()
                            })
                            .ToList()
                    })
                    .ToList()
            })
            .ToList();

        var allContents = topics
            .SelectMany(topic => topic.Contents)
            .ToList();

        var currentContent = contentId.HasValue
            ? allContents.FirstOrDefault(content => content.Id == contentId.Value)
            : allContents.FirstOrDefault();

        if (contentId.HasValue && currentContent is null)
        {
            return null;
        }

        return new CourseLearningDto
        {
            Id = course.Id,
            Title = course.Title,
            ShortDescription = course.ShortDescription,
            Description = course.Description,
            ImageUrl = course.ImageUrl,
            Category = course.Category,
            Level = course.Level,
            DurationHours = course.DurationHours,
            TotalPrice = topics.Sum(x => x.Price),
            TopicCount = topics.Count,
            ContentCount = allContents.Count,
            Topics = topics,
            CurrentContentId = currentContent?.Id,
            CurrentContent = currentContent
        };
    }
}
