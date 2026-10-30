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
        var chapterCounts = await _dbContext.Lessons
            .AsNoTracking()
            .Where(note => note.TopicId.HasValue && detailTopicIds.Contains(note.TopicId.Value) && note.Flag == 0)
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
        int? topicId = null,
        int? chapterId = null,
        bool includeAssessmentData = true)
    {
        var course = await _inner.GetLearningHeaderAsync(id, cancellationToken);
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
                CourseTopicId = courseTopic.Id,
                TopicId = topic.Id,
                TopicTitle = topic.Title,
                TopicPrice = topic.Price,
                TopicDurationMinutes = topic.DurationMinutes
            })
            .ToListAsync(cancellationToken);

        var topicData = topicRows
            .GroupBy(x => new
            {
                x.CourseTopicId,
                x.TopicId,
                x.TopicTitle,
                x.TopicPrice,
                x.TopicDurationMinutes
            })
            .Select(group => group.Key)
            .OrderBy(x => x.CourseTopicId)
            .ToList();

        var topicIds = topicData.Select(x => x.TopicId).ToList();

        var loadAllTopicDetails = !topicId.HasValue && !contentId.HasValue;

        // Load the shared lesson/chapter rows once. The previous two queries read the
        // same active lesson records with overlapping metadata.
        var lessonRows = topicIds.Count == 0
            ? []
            : await _dbContext.Lessons
                .AsNoTracking()
                .Where(lesson => lesson.Flag == 0
                    && lesson.TopicId.HasValue
                    && topicIds.Contains(lesson.TopicId.Value))
                .OrderBy(lesson => lesson.TopicId)
                .ThenBy(lesson => lesson.Id)
                .Select(lesson => new
                {
                    lesson.Id,
                    TopicId = lesson.TopicId,
                    NoteId = (int?)lesson.Id,
                    lesson.Title,
                    lesson.Slides,
                    lesson.VideoName,
                    ChapterId = lesson.Id.ToString(),
                    HtmlContent = loadAllTopicDetails ? lesson.HtmlContent : null,
                    HasNotes = lesson.HtmlContent != null && lesson.HtmlContent.Trim() != "",
                    SortOrder = lesson.Id
                })
                .ToListAsync(cancellationToken);

        var contentRows = lessonRows
            .Select(lesson => new
            {
                lesson.Id,
                lesson.TopicId,
                lesson.NoteId,
                lesson.Title,
                lesson.Slides,
                lesson.VideoName
            })
            .ToList();

        var currentContentRow = contentId.HasValue
            ? contentRows.FirstOrDefault(content => content.Id == contentId.Value)
            : null;
        var selectedTopicId = topicId
            ?? currentContentRow?.TopicId
            ?? topicData.FirstOrDefault()?.TopicId;

        // Keep only note metadata for all chapters; loading every chapter's rich HTML on
        // every refresh can dominate response time for courses with large notes.
        var chapterRows = lessonRows
            .Select(lesson => new
            {
                lesson.Id,
                TopicId = lesson.TopicId!.Value,
                lesson.ChapterId,
                lesson.Title,
                lesson.HtmlContent,
                lesson.HasNotes,
                lesson.SortOrder
            })
            .ToList();

        var selectedChapterId = chapterId
            ?? currentContentRow?.NoteId
            ?? chapterRows.FirstOrDefault(chapter => chapter.TopicId == selectedTopicId)?.Id;

        var selectedChapterHtml = !loadAllTopicDetails && selectedChapterId.HasValue
            ? await _dbContext.Lessons
                .AsNoTracking()
                .Where(note => note.Id == selectedChapterId.Value && note.Flag == 0)
                .Select(note => note.HtmlContent)
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        var contentIds = contentRows.Select(x => x.Id).Distinct().ToList();
        var noteIds = chapterRows.Select(x => x.Id).Distinct().ToList();
        var selectedTopicContentIds = selectedTopicId.HasValue
            ? contentRows.Where(content => content.TopicId == selectedTopicId.Value)
                .Select(content => content.Id).Distinct().ToList()
            : new List<int>();
        var selectedTopicNoteIds = selectedTopicId.HasValue
            ? chapterRows.Where(chapter => chapter.TopicId == selectedTopicId.Value)
                .Select(chapter => chapter.Id).Distinct().ToList()
            : new List<int>();

        // Fetch only lightweight question references for every topic. Full question text,
        // options, answers and practice descriptions are returned only for the open topic.
        var questionReferenceRows = new List<LearningQuestionReferenceRow>();
        if (includeAssessmentData)
        {
            questionReferenceRows = await _dbContext.ContentQuestions
                .AsNoTracking()
                .Where(question => question.Flag == 0
                    && ((question.ContentId.HasValue && contentIds.Contains(question.ContentId.Value))
                        || (question.TopicId.HasValue && topicIds.Contains(question.TopicId.Value))
                        || (question.NoteId.HasValue && noteIds.Contains(question.NoteId.Value))))
                .OrderBy(question => question.Id)
                .Select(question => new LearningQuestionReferenceRow(
                    question.Id,
                    question.NoteId,
                    question.ContentId,
                    question.TopicId,
                    loadAllTopicDetails
                        || (question.ContentId.HasValue && selectedTopicContentIds.Contains(question.ContentId.Value))
                        || (question.NoteId.HasValue && selectedTopicNoteIds.Contains(question.NoteId.Value))
                        || (selectedTopicId.HasValue && question.TopicId == selectedTopicId.Value),
                    (loadAllTopicDetails
                        || ((question.ContentId.HasValue && selectedTopicContentIds.Contains(question.ContentId.Value))
                            || (question.NoteId.HasValue && selectedTopicNoteIds.Contains(question.NoteId.Value))
                            || (selectedTopicId.HasValue && question.TopicId == selectedTopicId.Value)))
                        ? question.Question
                        : null,
                    (loadAllTopicDetails
                        || ((question.ContentId.HasValue && selectedTopicContentIds.Contains(question.ContentId.Value))
                            || (question.NoteId.HasValue && selectedTopicNoteIds.Contains(question.NoteId.Value))
                            || (selectedTopicId.HasValue && question.TopicId == selectedTopicId.Value)))
                        ? question.Option1
                        : null,
                    (loadAllTopicDetails
                        || ((question.ContentId.HasValue && selectedTopicContentIds.Contains(question.ContentId.Value))
                            || (question.NoteId.HasValue && selectedTopicNoteIds.Contains(question.NoteId.Value))
                            || (selectedTopicId.HasValue && question.TopicId == selectedTopicId.Value)))
                        ? question.Option2
                        : null,
                    (loadAllTopicDetails
                        || ((question.ContentId.HasValue && selectedTopicContentIds.Contains(question.ContentId.Value))
                            || (question.NoteId.HasValue && selectedTopicNoteIds.Contains(question.NoteId.Value))
                            || (selectedTopicId.HasValue && question.TopicId == selectedTopicId.Value)))
                        ? question.Option3
                        : null,
                    (loadAllTopicDetails
                        || ((question.ContentId.HasValue && selectedTopicContentIds.Contains(question.ContentId.Value))
                            || (question.NoteId.HasValue && selectedTopicNoteIds.Contains(question.NoteId.Value))
                            || (selectedTopicId.HasValue && question.TopicId == selectedTopicId.Value)))
                        ? question.Option4
                        : null))
                .ToListAsync(cancellationToken);
        }

        var questionReferences = questionReferenceRows
            .Select(question => new QuestionReference(question.Id, question.NoteId, question.ContentId, question.TopicId))
            .ToList();

        var selectedQuestionRows = questionReferenceRows
            .Where(question => question.IsSelectedTopic)
            .ToList();

        var interviewQuestionReferenceRows = new List<LearningInterviewQuestionReferenceRow>();
        if (includeAssessmentData)
        {
            interviewQuestionReferenceRows = await _dbContext.ContentInterviewQuestions
                .AsNoTracking()
                .Where(question => question.Flag == 0
                    && ((question.NoteId.HasValue && noteIds.Contains(question.NoteId.Value))
                        || (question.ContentId.HasValue && contentIds.Contains(question.ContentId.Value))
                        || (question.TopicId.HasValue && topicIds.Contains(question.TopicId.Value))))
                .OrderBy(question => question.Id)
                .Select(question => new LearningInterviewQuestionReferenceRow(
                    question.Id,
                    question.NoteId,
                    question.ContentId,
                    question.TopicId,
                    loadAllTopicDetails
                        || (question.NoteId.HasValue && selectedTopicNoteIds.Contains(question.NoteId.Value))
                        || (question.ContentId.HasValue && selectedTopicContentIds.Contains(question.ContentId.Value))
                        || (selectedTopicId.HasValue && question.TopicId == selectedTopicId.Value),
                    (loadAllTopicDetails
                        || ((question.NoteId.HasValue && selectedTopicNoteIds.Contains(question.NoteId.Value))
                            || (question.ContentId.HasValue && selectedTopicContentIds.Contains(question.ContentId.Value))
                            || (selectedTopicId.HasValue && question.TopicId == selectedTopicId.Value)))
                        ? question.Question ?? string.Empty
                        : string.Empty,
                    (loadAllTopicDetails
                        || ((question.NoteId.HasValue && selectedTopicNoteIds.Contains(question.NoteId.Value))
                            || (question.ContentId.HasValue && selectedTopicContentIds.Contains(question.ContentId.Value))
                            || (selectedTopicId.HasValue && question.TopicId == selectedTopicId.Value)))
                        ? question.Answer ?? string.Empty
                        : string.Empty))
                .ToListAsync(cancellationToken);
        }

        var interviewQuestionReferences = interviewQuestionReferenceRows
            .Select(question => new QuestionReference(question.Id, question.NoteId, question.ContentId, question.TopicId))
            .ToList();

        var interviewQuestions = interviewQuestionReferenceRows
            .Where(question => question.IsSelectedTopic)
            .ToList();

        var practiceQuestionReferenceRows = new List<LearningPracticeQuestionReferenceRow>();
        if (includeAssessmentData)
        {
            practiceQuestionReferenceRows = await _dbContext.ContentProgramQuestions
                .AsNoTracking()
                .Where(question => question.Flag == 0
                    && ((question.NoteId.HasValue && noteIds.Contains(question.NoteId.Value))
                        || (question.ContentId.HasValue && contentIds.Contains(question.ContentId.Value))))
                .OrderBy(question => question.Id)
                .Select(question => new LearningPracticeQuestionReferenceRow(
                    question.Id,
                    question.NoteId,
                    question.ContentId,
                    loadAllTopicDetails
                        || (question.NoteId.HasValue && selectedTopicNoteIds.Contains(question.NoteId.Value))
                        || (question.ContentId.HasValue && selectedTopicContentIds.Contains(question.ContentId.Value)),
                    loadAllTopicDetails
                        || (question.NoteId.HasValue && selectedTopicNoteIds.Contains(question.NoteId.Value))
                        || (question.ContentId.HasValue && selectedTopicContentIds.Contains(question.ContentId.Value))
                        ? question.QuestionTitle
                        : string.Empty,
                    loadAllTopicDetails
                        || (question.NoteId.HasValue && selectedTopicNoteIds.Contains(question.NoteId.Value))
                        || (question.ContentId.HasValue && selectedTopicContentIds.Contains(question.ContentId.Value))
                        ? question.QuestionDescription
                        : string.Empty))
                .ToListAsync(cancellationToken);
        }

        var practiceQuestionReferences = practiceQuestionReferenceRows
            .Select(question => new QuestionReference(question.Id, question.NoteId, question.ContentId, null))
            .ToList();

        var practiceQuestions = practiceQuestionReferenceRows
            .Where(question => question.IsSelectedTopic)
            .ToList();

        var questionsByContentId = selectedQuestionRows
            .Where(question => question.ContentId.HasValue)
            .GroupBy(question => question.ContentId!.Value)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ContentQuestionDto>)group.Select(question => MapLearningQuestion(
                    new QuestionReference(question.Id, question.NoteId, question.ContentId, question.TopicId),
                    question.Question, question.Option1, question.Option2, question.Option3, question.Option4)).ToList());

        var questionsByTopicId = selectedQuestionRows
            .Where(question => question.TopicId.HasValue)
            .GroupBy(question => question.TopicId!.Value)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ContentQuestionDto>)group.Select(question => MapLearningQuestion(
                    new QuestionReference(question.Id, question.NoteId, question.ContentId, question.TopicId),
                    question.Question, question.Option1, question.Option2, question.Option3, question.Option4)).ToList());

        var questionsByNoteId = selectedQuestionRows
            .Where(question => question.NoteId.HasValue)
            .GroupBy(question => question.NoteId!.Value)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ContentQuestionDto>)group.Select(question => MapLearningQuestion(
                    new QuestionReference(question.Id, question.NoteId, question.ContentId, question.TopicId),
                    question.Question, question.Option1, question.Option2, question.Option3, question.Option4)).ToList());

        var questionReferencesByContentId = questionReferences
            .Where(question => question.ContentId.HasValue)
            .GroupBy(question => question.ContentId!.Value)
            .ToDictionary(group => group.Key, group => group.ToList());

        var questionReferencesByTopicId = questionReferences
            .Where(question => question.TopicId.HasValue)
            .GroupBy(question => question.TopicId!.Value)
            .ToDictionary(group => group.Key, group => group.ToList());

        var questionReferencesByNoteId = questionReferences
            .Where(question => question.NoteId.HasValue)
            .GroupBy(question => question.NoteId!.Value)
            .ToDictionary(group => group.Key, group => group.ToList());

        var interviewReferencesByContentId = interviewQuestionReferences
            .Where(question => question.ContentId.HasValue)
            .GroupBy(question => question.ContentId!.Value)
            .ToDictionary(group => group.Key, group => group.ToList());

        var interviewReferencesByTopicId = interviewQuestionReferences
            .Where(question => question.TopicId.HasValue)
            .GroupBy(question => question.TopicId!.Value)
            .ToDictionary(group => group.Key, group => group.ToList());

        var interviewReferencesByNoteId = interviewQuestionReferences
            .Where(question => question.NoteId.HasValue)
            .GroupBy(question => question.NoteId!.Value)
            .ToDictionary(group => group.Key, group => group.ToList());

        var practiceReferencesByContentId = practiceQuestionReferences
            .Where(question => question.ContentId.HasValue)
            .GroupBy(question => question.ContentId!.Value)
            .ToDictionary(group => group.Key, group => group.ToList());

        var practiceReferencesByNoteId = practiceQuestionReferences
            .Where(question => question.NoteId.HasValue)
            .GroupBy(question => question.NoteId!.Value)
            .ToDictionary(group => group.Key, group => group.ToList());

        ContentQuestionDto MapLearningQuestion(QuestionReference questionReferenceRow, string? question, string? option1, string? option2, string? option3, string? option4)
        {
            return new ContentQuestionDto
            {
                Id = questionReferenceRow.Id,
                Question = question ?? string.Empty,
                Options = new[] { option1, option2, option3, option4 }
            };
        }
        var firstContentIdByTopicId = contentRows
            .Where(content => content.TopicId.HasValue)
            .GroupBy(content => content.TopicId!.Value)
            .ToDictionary(
                group => group.Key,
                group => group.Min(content => content.Id));

        var firstContentIdByNoteId = contentRows
            .GroupBy(content => content.Id)
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
            .GroupBy(content => content.Id)
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

        int CountQuestionReferencesForContent(
            int contentId,
            int? contentTopicId,
            int? noteId,
            Dictionary<int, List<QuestionReference>> byContentId,
            Dictionary<int, List<QuestionReference>> byTopicId,
            Dictionary<int, List<QuestionReference>> byNoteId)
        {
            var ids = new HashSet<int>();

            if (byContentId.TryGetValue(contentId, out var directQuestions))
            {
                ids.UnionWith(directQuestions.Select(question => question.Id));
            }

            if (contentTopicId.HasValue
                && firstContentIdByTopicId.TryGetValue(contentTopicId.Value, out var firstTopicContentId)
                && firstTopicContentId == contentId
                && byTopicId.TryGetValue(contentTopicId.Value, out var topicQuestions))
            {
                ids.UnionWith(topicQuestions.Select(question => question.Id));
            }

            if (noteId.HasValue
                && firstContentIdByNoteId.TryGetValue(noteId.Value, out var firstNoteContentId)
                && firstNoteContentId == contentId
                && byNoteId.TryGetValue(noteId.Value, out var noteQuestions))
            {
                ids.UnionWith(noteQuestions.Select(question => question.Id));
            }

            return ids.Count;
        }

        int CountQuestionReferencesForChapter(
            int chapterId,
            int chapterTopicId,
            Dictionary<int, List<QuestionReference>> byNoteId,
            Dictionary<int, List<QuestionReference>> byContentId,
            Dictionary<int, List<QuestionReference>>? byTopicId)
        {
            var ids = new HashSet<int>();

            if (byNoteId.TryGetValue(chapterId, out var noteQuestions))
            {
                ids.UnionWith(noteQuestions.Select(question => question.Id));
            }

            if (contentRowsByNoteId.TryGetValue(chapterId, out var noteContents))
            {
                foreach (var content in noteContents)
                {
                    if (byContentId.TryGetValue(content.Id, out var contentQuestions))
                    {
                        ids.UnionWith(contentQuestions.Select(question => question.Id));
                    }
                }
            }

            if (byTopicId is not null
                && firstChapterIdByTopicId.TryGetValue(chapterTopicId, out var firstChapterId)
                && firstChapterId == chapterId
                && byTopicId.TryGetValue(chapterTopicId, out var topicQuestions))
            {
                ids.UnionWith(topicQuestions.Select(question => question.Id));
            }

            return ids.Count;
        }

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
                            NoteId = content.Id,
                            Title = content.Title ?? string.Empty,
                            Slides = content.Slides,
                            VideoName = content.VideoName,
                            QuestionCount = CountQuestionReferencesForContent(
                                content.Id,
                                content.TopicId,
                                content.Id,
                                questionReferencesByContentId,
                                questionReferencesByTopicId,
                                questionReferencesByNoteId),
                            Questions = GetQuestionsForContent(content.Id, content.TopicId, content.Id)
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
                            QuestionCount = CountQuestionReferencesForChapter(
                                chapter.Id,
                                topic.TopicId,
                                questionReferencesByNoteId,
                                questionReferencesByContentId,
                                questionReferencesByTopicId),
                            InterviewQuestionCount = CountQuestionReferencesForChapter(
                                chapter.Id,
                                topic.TopicId,
                                interviewReferencesByNoteId,
                                interviewReferencesByContentId,
                                interviewReferencesByTopicId),
                            PracticeQuestionCount = CountQuestionReferencesForChapter(
                                chapter.Id,
                                topic.TopicId,
                                practiceReferencesByNoteId,
                                practiceReferencesByContentId,
                                null),
                            NotesHtml = loadAllTopicDetails
                                ? chapter.HtmlContent ?? string.Empty
                                : chapter.Id == selectedChapterId
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
                                        QuestionCount = CountQuestionReferencesForContent(
                                            content.Id,
                                            content.TopicId,
                                            content.Id,
                                            questionReferencesByContentId,
                                            questionReferencesByTopicId,
                                            questionReferencesByNoteId),
                                        Questions = GetQuestionsForContent(content.Id, content.TopicId, content.Id)
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
            : chapterId.HasValue
                ? null
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
            CurrentChapterId = chapterId,
            CurrentContent = currentContent
        };
    }

    private sealed record LearningQuestionReferenceRow(
        int Id,
        int? NoteId,
        int? ContentId,
        int? TopicId,
        bool IsSelectedTopic,
        string? Question,
        string? Option1,
        string? Option2,
        string? Option3,
        string? Option4);

    private sealed record LearningInterviewQuestionReferenceRow(
        int Id,
        int? NoteId,
        int? ContentId,
        int? TopicId,
        bool IsSelectedTopic,
        string Question,
        string Answer);

    private sealed record LearningPracticeQuestionReferenceRow(
        int Id,
        int? NoteId,
        int? ContentId,
        bool IsSelectedTopic,
        string? QuestionTitle,
        string? QuestionDescription);

    private sealed record QuestionReference(
        int Id,
        int? NoteId,
        int? ContentId,
        int? TopicId);
}
