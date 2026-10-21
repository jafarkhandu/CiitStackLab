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
                DurationMinutes = topic.DurationMinutes,
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
        CancellationToken cancellationToken = default,
        int? topicId = null)
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
            : await (
                from lesson in _dbContext.Lessons.AsNoTracking()
                join note in _dbContext.TrainingNotes.AsNoTracking()
                    on lesson.NoteId equals (int?)note.Id into noteJoin
                from note in noteJoin.DefaultIfEmpty()
                where lesson.Flag == 0
                    && ((lesson.TopicId.HasValue && topicIds.Contains(lesson.TopicId.Value))
                        || (lesson.NoteId.HasValue
                            && note != null
                            && note.Flag == 0
                            && topicIds.Contains(note.TopicId)))
                orderby (lesson.TopicId ?? (int?)note.TopicId), lesson.Id
                select new
                {
                    lesson.Id,
                    TopicId = lesson.TopicId ?? (int?)note.TopicId,
                    lesson.NoteId,
                    lesson.Title,
                    lesson.Slides,
                    lesson.VideoName
                })
                .Distinct()
                .ToListAsync(cancellationToken);

        var currentContentRow = contentId.HasValue
            ? contentRows.FirstOrDefault(content => content.Id == contentId.Value)
            : null;
        var selectedTopicId = topicId
            ?? currentContentRow?.TopicId
            ?? topicData.FirstOrDefault()?.TopicId;

        // Keep only note metadata for all chapters; loading every chapter's rich HTML on
        // every refresh can dominate response time for courses with large notes.
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
                    HasNotes = note.HtmlContent != null && note.HtmlContent.Trim() != "",
                    note.SortOrder
                })
                .ToListAsync(cancellationToken);

        var selectedChapterId = currentContentRow?.NoteId
            ?? chapterRows.FirstOrDefault(chapter => chapter.TopicId == selectedTopicId)?.Id;

        var selectedChapterHtml = selectedChapterId.HasValue
            ? await _dbContext.TrainingNotes
                .AsNoTracking()
                .Where(note => note.Id == selectedChapterId.Value && note.Flag == 0)
                .Select(note => note.HtmlContent)
                .FirstOrDefaultAsync(cancellationToken)
            : null;

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

        // Pre-group data once. The learning page renders every topic in its sidebar,
        // so filtering the full course collections again for every topic/chapter is costly.
        var contentRowsByTopicId = contentRows
            .Where(content => content.TopicId.HasValue)
            .GroupBy(content => content.TopicId!.Value)
            .ToDictionary(group => group.Key, group => group.ToList());

        var contentRowsByNoteId = contentRows
            .Where(content => content.NoteId.HasValue)
            .GroupBy(content => content.NoteId!.Value)
            .ToDictionary(group => group.Key, group => group.ToList());

        var chapterRowsByTopicId = chapterRows
            .GroupBy(chapter => chapter.TopicId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var firstChapterIdByTopicId = chapterRows
            .GroupBy(chapter => chapter.TopicId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(chapter => chapter.SortOrder)
                    .ThenBy(chapter => chapter.Id)
                    .First().Id);

        var interviewQuestionsByNoteId = interviewQuestions
            .Where(question => question.NoteId.HasValue)
            .GroupBy(question => question.NoteId!.Value)
            .ToDictionary(group => group.Key, group => group.ToList());

        var interviewQuestionsByContentId = interviewQuestions
            .Where(question => question.ContentId.HasValue)
            .GroupBy(question => question.ContentId!.Value)
            .ToDictionary(group => group.Key, group => group.ToList());

        var interviewQuestionsByTopicId = interviewQuestions
            .Where(question => question.TopicId.HasValue)
            .GroupBy(question => question.TopicId!.Value)
            .ToDictionary(group => group.Key, group => group.ToList());

        var practiceQuestionsByNoteId = practiceQuestions
            .Where(question => question.NoteId.HasValue)
            .GroupBy(question => question.NoteId!.Value)
            .ToDictionary(group => group.Key, group => group.ToList());

        var practiceQuestionsByContentId = practiceQuestions
            .Where(question => question.ContentId.HasValue)
            .GroupBy(question => question.ContentId!.Value)
            .ToDictionary(group => group.Key, group => group.ToList());

        IReadOnlyList<ContentQuestionDto> GetQuestionsForContent(int contentId, int? topicId, int? noteId)
        {
            var mappedQuestions = new Dictionary<int, ContentQuestionDto>();

            if (questionsByContentId.TryGetValue(contentId, out var contentQuestions))
            {
                foreach (var question in contentQuestions)
                {
                    mappedQuestions.TryAdd(question.Id, question);
                }
            }

            if (topicId.HasValue
                && firstContentIdByTopicId.TryGetValue(topicId.Value, out var firstContentId)
                && firstContentId == contentId
                && questionsByTopicId.TryGetValue(topicId.Value, out var topicQuestions))
            {
                foreach (var question in topicQuestions)
                {
                    mappedQuestions.TryAdd(question.Id, question);
                }
            }

            if (noteId.HasValue
                && firstContentIdByNoteId.TryGetValue(noteId.Value, out var firstChapterContentId)
                && firstChapterContentId == contentId
                && questionsByNoteId.TryGetValue(noteId.Value, out var chapterQuestions))
            {
                foreach (var question in chapterQuestions)
                {
                    mappedQuestions.TryAdd(question.Id, question);
                }
            }

            return mappedQuestions.Values.OrderBy(question => question.Id).ToList();
        }

        IReadOnlyList<ContentQuestionDto> GetQuestionsForChapter(int chapterId, int topicIdForChapter)
        {
            var mappedQuestions = new Dictionary<int, ContentQuestionDto>();

            if (questionsByNoteId.TryGetValue(chapterId, out var noteQuestions))
            {
                foreach (var question in noteQuestions)
                {
                    mappedQuestions.TryAdd(question.Id, question);
                }
            }

            if (contentRowsByNoteId.TryGetValue(chapterId, out var noteContents))
            {
                foreach (var content in noteContents)
                {
                    if (!questionsByContentId.TryGetValue(content.Id, out var contentQuestions))
                    {
                        continue;
                    }

                    foreach (var question in contentQuestions)
                    {
                        mappedQuestions.TryAdd(question.Id, question);
                    }
                }
            }

            if (firstChapterIdByTopicId.TryGetValue(topicIdForChapter, out var firstChapterId)
                && firstChapterId == chapterId
                && questionsByTopicId.TryGetValue(topicIdForChapter, out var topicQuestions))
            {
                foreach (var question in topicQuestions)
                {
                    mappedQuestions.TryAdd(question.Id, question);
                }
            }

            return mappedQuestions.Values.OrderBy(question => question.Id).ToList();
        }

        IReadOnlyList<CourseLearningInterviewQuestionDto> GetInterviewQuestionsForChapter(
            int chapterId,
            int topicIdForChapter)
        {
            var mappedQuestions = new Dictionary<int, CourseLearningInterviewQuestionDto>();

            if (interviewQuestionsByNoteId.TryGetValue(chapterId, out var noteQuestions))
            {
                foreach (var question in noteQuestions)
                {
                    mappedQuestions.TryAdd(question.Id, new CourseLearningInterviewQuestionDto
                    {
                        Id = question.Id,
                        Question = question.Question,
                        Answer = question.Answer
                    });
                }
            }

            if (contentRowsByNoteId.TryGetValue(chapterId, out var noteContents))
            {
                foreach (var content in noteContents)
                {
                    if (!interviewQuestionsByContentId.TryGetValue(content.Id, out var contentQuestions))
                    {
                        continue;
                    }

                    foreach (var question in contentQuestions)
                    {
                        mappedQuestions.TryAdd(question.Id, new CourseLearningInterviewQuestionDto
                        {
                            Id = question.Id,
                            Question = question.Question,
                            Answer = question.Answer
                        });
                    }
                }
            }

            if (firstChapterIdByTopicId.TryGetValue(topicIdForChapter, out var firstChapterId)
                && firstChapterId == chapterId
                && interviewQuestionsByTopicId.TryGetValue(topicIdForChapter, out var topicQuestions))
            {
                foreach (var question in topicQuestions)
                {
                    mappedQuestions.TryAdd(question.Id, new CourseLearningInterviewQuestionDto
                    {
                        Id = question.Id,
                        Question = question.Question,
                        Answer = question.Answer
                    });
                }
            }

            return mappedQuestions.Values.OrderBy(question => question.Id).ToList();
        }

        IReadOnlyList<CourseLearningPracticeQuestionDto> GetPracticeQuestionsForChapter(int chapterId)
        {
            var mappedQuestions = new Dictionary<int, CourseLearningPracticeQuestionDto>();

            if (practiceQuestionsByNoteId.TryGetValue(chapterId, out var noteQuestions))
            {
                foreach (var question in noteQuestions)
                {
                    mappedQuestions.TryAdd(question.Id, new CourseLearningPracticeQuestionDto
                    {
                        Id = question.Id,
                        QuestionTitle = question.QuestionTitle,
                        QuestionDescription = question.QuestionDescription
                    });
                }
            }

            if (contentRowsByNoteId.TryGetValue(chapterId, out var noteContents))
            {
                foreach (var content in noteContents)
                {
                    if (!practiceQuestionsByContentId.TryGetValue(content.Id, out var contentQuestions))
                    {
                        continue;
                    }

                    foreach (var question in contentQuestions)
                    {
                        mappedQuestions.TryAdd(question.Id, new CourseLearningPracticeQuestionDto
                        {
                            Id = question.Id,
                            QuestionTitle = question.QuestionTitle,
                            QuestionDescription = question.QuestionDescription
                        });
                    }
                }
            }

            return mappedQuestions.Values.OrderBy(question => question.Id).ToList();
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
            .Select(topic =>
            {
                var topicContents = contentRowsByTopicId.TryGetValue(topic.TopicId, out var groupedContents)
                    ? groupedContents
                    : contentRows.Where(content => content.TopicId == topic.TopicId).ToList();

                var topicChapters = chapterRowsByTopicId.TryGetValue(topic.TopicId, out var groupedChapters)
                    ? groupedChapters
                    : chapterRows.Where(chapter => chapter.TopicId == topic.TopicId).ToList();

                return new CourseLearningTopicDto
                {
                    Id = topic.TopicId,
                    Title = topic.TopicTitle,
                    Price = topic.TopicPrice,
                    DurationMinutes = topic.TopicDurationMinutes,
                    Contents = topicContents
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
                    Chapters = topicChapters
                        .Select(chapter => new CourseLearningChapterDto
                        {
                            Id = chapter.Id,
                            ChapterId = chapter.ChapterId,
                            Title = chapter.Title ?? string.Empty,
                            SortOrder = chapter.SortOrder,
                            HasNotes = chapter.HasNotes,
                            NotesHtml = chapter.Id == selectedChapterId
                                ? selectedChapterHtml ?? string.Empty
                                : string.Empty,
                            Questions = GetQuestionsForChapter(chapter.Id, topic.TopicId),
                            InterviewQuestions = GetInterviewQuestionsForChapter(chapter.Id, topic.TopicId),
                            PracticeQuestions = GetPracticeQuestionsForChapter(chapter.Id),
                            Contents = contentRowsByNoteId.TryGetValue(chapter.Id, out var chapterContents)
                                ? chapterContents
                                    .Where(content => content.TopicId == topic.TopicId)
                                    .Select(content => new CourseLearningContentDto
                                    {
                                        Id = content.Id,
                                        Title = content.Title ?? string.Empty,
                                        Slides = content.Slides,
                                        VideoName = content.VideoName,
                                        Questions = GetQuestionsForContent(content.Id, content.TopicId, content.NoteId)
                                    })
                                    .ToList()
                                : new List<CourseLearningContentDto>()
                        })
                        .ToList()
                };
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
            DurationHours = topics.Sum(topic => topic.DurationMinutes ?? 0) / 60m,
            TotalPrice = topics.Sum(x => Math.Max(0m, x.Price)),
            TopicCount = topics.Count,
            ContentCount = allContents.Count,
            Topics = topics,
            CurrentContentId = currentContent?.Id,
            CurrentContent = currentContent
        };
    }
}
