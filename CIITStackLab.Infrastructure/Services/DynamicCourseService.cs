using CIITStackLab.Domain.Entities;
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

        var detailTopicIds = details.Topics.Select(topic => topic.Id).ToList();
        var chapterCounts = await _dbContext.TrainingNotes
            .AsNoTracking()
            .Where(note => detailTopicIds.Contains(note.TopicId) && note.Flag == 0)
            .GroupBy(note => note.TopicId)
            .Select(group => new { TopicId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.TopicId, item => item.Count, cancellationToken);

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
            TotalPrice = details.TotalPrice,
            Topics = details.Topics.Select(topic => new CourseTopicDto
            {
                Id = topic.Id,
                Title = topic.Title,
                Price = topic.Price,
                HasNotes = topic.HasNotes,
                ChapterCount = chapterCounts.TryGetValue(topic.Id, out var chapterCount) ? chapterCount : 0,
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
                    note.HtmlContent,
                    note.SortOrder
                })
                .ToListAsync(cancellationToken);

        var contentIds = contentRows.Select(x => x.Id).Distinct().ToList();
        var noteIds = chapterRows.Select(x => x.Id).Distinct().ToList();

        var interviewQuestions = await _dbContext.ContentInterviewQuestions
            .AsNoTracking()
            .Where(question => question.Flag == 0
                && ((question.NoteId.HasValue && noteIds.Contains(question.NoteId.Value))
                    || (question.ContentId.HasValue && contentIds.Contains(question.ContentId.Value))
                    || (question.TopicId.HasValue && topicIds.Contains(question.TopicId.Value))))
            .OrderBy(question => question.Id)
            .Select(question => new
            {
                question.Id,
                question.NoteId,
                question.ContentId,
                question.TopicId,
                Question = question.Question ?? string.Empty,
                Answer = question.Answer ?? string.Empty
            })
            .ToListAsync(cancellationToken);

        var practiceQuestions = await _dbContext.ContentProgramQuestions
            .AsNoTracking()
            .Where(question => question.Flag == 0
                && ((question.NoteId.HasValue && noteIds.Contains(question.NoteId.Value))
                    || (question.ContentId.HasValue && contentIds.Contains(question.ContentId.Value))))
            .OrderBy(question => question.Id)
            .Select(question => new
            {
                question.Id,
                question.NoteId,
                question.ContentId,
                question.QuestionTitle,
                question.QuestionDescription
            })
            .ToListAsync(cancellationToken);

        var questions = await _dbContext.ContentQuestions
            .AsNoTracking()
            .Where(question =>
                question.Flag == 0
                && ((question.ContentId.HasValue
                     && contentIds.Contains(question.ContentId.Value))
                    || (question.TopicId.HasValue
                        && topicIds.Contains(question.TopicId.Value))
                    || (question.NoteId.HasValue
                        && noteIds.Contains(question.NoteId.Value))))
            .OrderBy(question => question.Id)
            .ToListAsync(cancellationToken);

        var questionsByContentId = questions
            .Where(question => question.ContentId.HasValue)
            .GroupBy(question => question.ContentId!.Value)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ContentQuestionDto>)group.Select(MapLearningQuestion).ToList());

        var questionsByTopicId = questions
            .Where(question => question.TopicId.HasValue)
            .GroupBy(question => question.TopicId!.Value)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ContentQuestionDto>)group.Select(MapLearningQuestion).ToList());

        var questionsByNoteId = questions
            .Where(question => question.NoteId.HasValue)
            .GroupBy(question => question.NoteId!.Value)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ContentQuestionDto>)group.Select(MapLearningQuestion).ToList());

        var firstContentIdByTopicId = contentRows
            .Where(content => content.TopicId.HasValue)
            .GroupBy(content => content.TopicId!.Value)
            .ToDictionary(
                group => group.Key,
                group => group.Min(content => content.Id));

        var firstContentIdByNoteId = contentRows
            .Where(content => content.NoteId.HasValue)
            .GroupBy(content => content.NoteId!.Value)
            .ToDictionary(
                group => group.Key,
                group => group.Min(content => content.Id));

        IReadOnlyList<ContentQuestionDto> GetQuestionsForContent(int contentId, int? topicId, int? noteId)
        {
            var mappedQuestions = new List<ContentQuestionDto>();

            if (questionsByContentId.TryGetValue(contentId, out var contentQuestions))
            {
                mappedQuestions.AddRange(contentQuestions);
            }

            if (topicId.HasValue
                && firstContentIdByTopicId.TryGetValue(topicId.Value, out var firstContentId)
                && firstContentId == contentId
                && questionsByTopicId.TryGetValue(topicId.Value, out var topicQuestions))
            {
                foreach (var question in topicQuestions)
                {
                    if (mappedQuestions.All(existingQuestion => existingQuestion.Id != question.Id))
                    {
                        mappedQuestions.Add(question);
                    }
                }
            }

            if (noteId.HasValue
                && firstContentIdByNoteId.TryGetValue(noteId.Value, out var firstChapterContentId)
                && firstChapterContentId == contentId
                && questionsByNoteId.TryGetValue(noteId.Value, out var chapterQuestions))
            {
                foreach (var question in chapterQuestions)
                {
                    if (mappedQuestions.All(existingQuestion => existingQuestion.Id != question.Id))
                    {
                        mappedQuestions.Add(question);
                    }
                }
            }

            return mappedQuestions;
        }

        ContentQuestionDto MapLearningQuestion(ContentQuestion question)
        {
            return new ContentQuestionDto
            {
                Id = question.Id,
                Question = question.Question ?? string.Empty,
                Options = new[] { question.Option1, question.Option2, question.Option3, question.Option4 }
            };
        }

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
                        NoteId = content.NoteId,
                        Title = content.Title ?? string.Empty,
                        Slides = content.Slides,
                        VideoName = content.VideoName,
                        Questions = GetQuestionsForContent(content.Id, content.TopicId, content.NoteId)
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
                        NotesHtml = chapter.HtmlContent ?? string.Empty,
                        Questions = questions
                            .Where(question => question.NoteId == chapter.Id
                                || (question.ContentId.HasValue
                                    && contentRows.Any(content => content.Id == question.ContentId.Value
                                        && content.NoteId == chapter.Id))
                                || (question.TopicId == topic.TopicId
                                    && chapter.Id == chapterRows
                                        .Where(candidate => candidate.TopicId == topic.TopicId)
                                        .OrderBy(candidate => candidate.SortOrder)
                                        .ThenBy(candidate => candidate.Id)
                                        .Select(candidate => candidate.Id)
                                        .FirstOrDefault()))
                            .Select(MapLearningQuestion)
                            .ToList(),
                        InterviewQuestions = interviewQuestions
                            .Where(question => question.NoteId == chapter.Id
                                || (question.ContentId.HasValue
                                    && contentRows.Any(content => content.Id == question.ContentId.Value
                                        && content.NoteId == chapter.Id)))
                            .Select(question => new CourseLearningInterviewQuestionDto
                            {
                                Id = question.Id,
                                Question = question.Question,
                                Answer = question.Answer
                            })
                            .ToList(),
                        PracticeQuestions = practiceQuestions
                            .Where(question => question.NoteId == chapter.Id
                                || (question.ContentId.HasValue
                                    && contentRows.Any(content => content.Id == question.ContentId.Value
                                        && content.NoteId == chapter.Id)))
                            .Select(question => new CourseLearningPracticeQuestionDto
                            {
                                Id = question.Id,
                                QuestionTitle = question.QuestionTitle,
                                QuestionDescription = question.QuestionDescription
                            })
                            .ToList(),
                        Contents = contentRows
                            .Where(content => content.TopicId == topic.TopicId
                                              && content.NoteId == chapter.Id)
                            .Select(content => new CourseLearningContentDto
                            {
                                Id = content.Id,
                                Title = content.Title ?? string.Empty,
                                Slides = content.Slides,
                                VideoName = content.VideoName,
                                Questions = GetQuestionsForContent(content.Id, content.TopicId, content.NoteId)
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
            TotalPrice = topics.Sum(x => Math.Max(0m, x.Price)),
            TopicCount = topics.Count,
            ContentCount = allContents.Count,
            Topics = topics,
            CurrentContentId = currentContent?.Id,
            CurrentContent = currentContent
        };
    }
}
