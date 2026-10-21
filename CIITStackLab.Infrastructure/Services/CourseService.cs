using CIITStackLab.Domain.Entities;
using CIITStackLab.Application.DTOs;
using CIITStackLab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CIITStackLab.Infrastructure.Services;

public sealed class CourseService
{
    private static readonly IReadOnlyDictionary<string, CoursePresentation> Presentations =
        new Dictionary<string, CoursePresentation>(StringComparer.OrdinalIgnoreCase)
        {
            ["Java Full Stack"] = new(
                "Learn Java, JDBC and many more..",
                "Build Java applications with strong programming, object-oriented and full-stack development fundamentals.",
                "https://tutorial.ciitstudent.com/Course_img/b0998bd1-f159-4388-94ea-66a4be8d1ab2.png",
                "Full Stack",
                "Beginner",
                45),
            ["Python Full Stack"] = new(
                "Learn Python, Django, FastApi and many more.",
                "Build Python applications and progress from programming fundamentals to full-stack development.",
                "https://tutorial.ciitstudent.com/Course_img/Python_full_stack.png",
                "Full Stack",
                "Intermediate",
                60),
            [".Net Full Stack"] = new(
                "Build applications using C#, .NET Core and many more.",
                "Learn C#, ASP.NET Core, Entity Framework Core and modern full-stack .NET application development.",
                "https://tutorial.ciitstudent.com/Course_img/DotNet_Full_Stack.png",
                "Full Stack",
                "Intermediate",
                60),
            ["DevOps"] = new(
                "Learn Jira, Github, Jenkins, AWS, Azure and many more.",
                "Learn source control, CI/CD, containers, cloud platforms and practical DevOps workflows.",
                "https://tutorial.ciitstudent.com/Course_img/DevOps.png",
                "DevOps",
                "Intermediate",
                45),
            ["Data Analytics"] = new(
                "Learn python, RDBMS, Excel, PowerBI and many more.",
                "Learn data preparation, SQL, Excel, Python and dashboarding for practical analytics.",
                "https://tutorial.ciitstudent.com/Course_img/Data_Analytics.png",
                "Data",
                "Beginner",
                40),
            ["Data Science"] = new(
                "Learn python, AI, ML, Gen AI, LLM, RAG, Agentic AI and many more.",
                "Explore statistics, machine learning, generative AI and practical data science workflows.",
                "https://tutorial.ciitstudent.com/Course_img/Data_Science.png",
                "Data",
                "Intermediate",
                55),
            ["Software Testing"] = new(
                "Learn Core Java, Manual Testing, Selenium, Playwright and many more.",
                "Learn manual testing and automation with Java, Selenium and Playwright.",
                "https://tutorial.ciitstudent.com/Course_img/748ca555-f359-43c7-838a-4104184ec450.png",
                "Testing",
                "Beginner",
                45),
            ["DevOps with Gen AI"] = new(
                "Learn Azure, AWS DevOps, Gen AI and many more.",
                "Combine cloud DevOps practices with modern generative AI workflows and automation.",
                "https://tutorial.ciitstudent.com/Course_img/5ae4054a-8bdd-4b84-b913-31a4c879c99f.png",
                "DevOps",
                "Advanced",
                55),
            ["Full Stack With DevOps"] = new(
                "Learn Full Stack, Azure, AWS DevOps, Gen AI and many more.",
                "Learn complete full-stack development together with cloud, DevOps and generative AI practices.",
                "https://tutorial.ciitstudent.com/Course_img/Full_Stack_With_DevOps.png",
                "Full Stack",
                "Advanced",
                75)
        };

    private readonly ApplicationDbContext _dbContext;

    public CourseService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<CourseDto>> GetPublishedAsync(
        CancellationToken cancellationToken = default)
    {
        var courses = await _dbContext.Courses
            .AsNoTracking()
            .Where(x => x.Flag == 0)
            .OrderBy(x => x.Title)
            .ToListAsync(cancellationToken);

        var durationHoursByCourseId = await GetDurationHoursByCourseIdsAsync(
            courses.Select(course => course.Id).ToList(),
            cancellationToken);

        return courses
            .Select(course => Map(
                course,
                durationHoursByCourseId.TryGetValue(course.Id, out var durationHours)
                    ? durationHours
                    : 0m))
            .ToList();
    }

    public async Task<CourseDto?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var course = await _dbContext.Courses
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == id && x.Flag == 0,
                cancellationToken);

        if (course is null)
        {
            return null;
        }

        var durationHoursByCourseId = await GetDurationHoursByCourseIdsAsync(
            new[] { course.Id },
            cancellationToken);

        return Map(
            course,
            durationHoursByCourseId.TryGetValue(course.Id, out var durationHours)
                ? durationHours
                : 0m);
    }

    public async Task<CourseDetailsDto?> GetDetailsAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var course = await _dbContext.Courses
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == id && x.Flag == 0,
                cancellationToken);

        if (course is null)
        {
            return null;
        }

        var rows = await (
            from courseTopic in _dbContext.CourseModules.AsNoTracking()
            join topic in _dbContext.Topics.AsNoTracking()
                on courseTopic.TopicId equals topic.Id
            join content in _dbContext.Lessons.AsNoTracking()
                on topic.Id equals content.TopicId into contentGroup
            from content in contentGroup.DefaultIfEmpty()
            where courseTopic.CourseId == id
                  && courseTopic.Flag == 0
                  && topic.Flag == 0
                  && (content == null || content.Flag == 0)
            orderby topic.Id, content.Id
            select new
            {
                TopicId = topic.Id,
                TopicTitle = topic.Title,
                TopicPrice = topic.Price,
                TopicDurationMinutes = topic.DurationMinutes,
                ContentId = (int?)content.Id,
                NoteId = (int?)content.NoteId,
                ContentTitle = content.Title,
                Slides = content.Slides,
                VideoName = content.VideoName
            })
            .ToListAsync(cancellationToken);

        var topicIds = rows
            .Select(x => x.TopicId)
            .Distinct()
            .ToList();

        var topicsWithNotes = topicIds.Count == 0
            ? new HashSet<int>()
            : new HashSet<int>(await _dbContext.TrainingNotes
                .AsNoTracking()
                .Where(note => topicIds.Contains(note.TopicId) && (note.Flag == 0 || note.Flag == null))
                .Select(note => note.TopicId)
                .Distinct()
                .ToListAsync(cancellationToken));

        var chapterRows = topicIds.Count == 0
            ? new List<(int TopicId, string Title)>()
            : (await _dbContext.TrainingNotes
                .AsNoTracking()
                .Where(note => topicIds.Contains(note.TopicId) && (note.Flag == 0 || note.Flag == null))
                .OrderBy(note => note.TopicId)
                .ThenBy(note => note.SortOrder)
                .ThenBy(note => note.Id)
                .Select(note => new { note.TopicId, note.Title })
                .ToListAsync(cancellationToken))
                .Select(note => (note.TopicId, note.Title))
                .ToList();

        var contentIds = rows
            .Where(x => x.ContentId.HasValue)
            .Select(x => x.ContentId!.Value)
            .Distinct()
            .ToList();
        var chapterIds = topicIds.Count == 0
            ? new List<int>()
            : await _dbContext.TrainingNotes.AsNoTracking()
                .Where(note => topicIds.Contains(note.TopicId) && (note.Flag == 0 || note.Flag == null))
                .Select(note => note.Id)
                .ToListAsync(cancellationToken);
        var noteIds = rows
            .Where(x => x.NoteId.HasValue)
            .Select(x => x.NoteId!.Value)
            .Concat(chapterIds)
            .Distinct()
            .ToList();

        var topicIdByContentId = rows
            .Where(row => row.ContentId.HasValue)
            .GroupBy(row => row.ContentId!.Value)
            .ToDictionary(group => group.Key, group => group.First().TopicId);
        var topicIdByChapterId = topicIds.Count == 0
            ? new Dictionary<int, int>()
            : await _dbContext.TrainingNotes.AsNoTracking()
                .Where(note => topicIds.Contains(note.TopicId) && (note.Flag == 0 || note.Flag == null))
                .ToDictionaryAsync(note => note.Id, note => note.TopicId, cancellationToken);

        var interviewRows = await _dbContext.ContentInterviewQuestions.AsNoTracking()
            .Where(question => (question.Flag == 0 || question.Flag == null)
                && ((question.TopicId.HasValue && topicIds.Contains(question.TopicId.Value))
                    || (question.ContentId.HasValue && contentIds.Contains(question.ContentId.Value))
                    || (question.NoteId.HasValue && chapterIds.Contains(question.NoteId.Value))))
            .Select(question => new { question.Id, question.TopicId, question.ContentId, question.NoteId })
            .ToListAsync(cancellationToken);

        var practiceRows = await _dbContext.ContentProgramQuestions.AsNoTracking()
            .Where(question => (question.Flag == 0 || question.Flag == null)
                && ((question.ContentId.HasValue && contentIds.Contains(question.ContentId.Value))
                    || (question.NoteId.HasValue && chapterIds.Contains(question.NoteId.Value))))
            .Select(question => new { question.Id, question.ContentId, question.NoteId })
            .ToListAsync(cancellationToken);

        var questions = await _dbContext.ContentQuestions
            .AsNoTracking()
            .Where(question =>
                (question.Flag == 0 || question.Flag == null)
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
                group => (IReadOnlyList<ContentQuestionDto>)group.Select(MapCourseQuestion).ToList());

        var questionsByTopicId = questions
            .Where(question => question.TopicId.HasValue)
            .GroupBy(question => question.TopicId!.Value)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ContentQuestionDto>)group.Select(MapCourseQuestion).ToList());

        var questionsByNoteId = questions
            .Where(question => question.NoteId.HasValue)
            .GroupBy(question => question.NoteId!.Value)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ContentQuestionDto>)group.Select(MapCourseQuestion).ToList());

        var firstContentIdByTopicId = rows
            .Where(row => row.ContentId.HasValue)
            .GroupBy(row => row.TopicId)
            .ToDictionary(
                group => group.Key,
                group => group.Min(row => row.ContentId!.Value));

        var firstContentIdByNoteId = rows
            .Where(row => row.ContentId.HasValue && row.NoteId.HasValue)
            .GroupBy(row => row.NoteId!.Value)
            .ToDictionary(
                group => group.Key,
                group => group.Min(row => row.ContentId!.Value));

        IReadOnlyList<ContentQuestionDto> GetQuestionsForCourseContent(int contentId, int topicId, int? noteId)
        {
            var result = new List<ContentQuestionDto>();

            if (questionsByContentId.TryGetValue(contentId, out var contentQuestions))
            {
                result.AddRange(contentQuestions);
            }

            if (firstContentIdByTopicId.TryGetValue(topicId, out var firstContentId)
                && firstContentId == contentId
                && questionsByTopicId.TryGetValue(topicId, out var topicQuestions))
            {
                foreach (var question in topicQuestions)
                {
                    if (result.All(existingQuestion => existingQuestion.Id != question.Id))
                    {
                        result.Add(question);
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
                    if (result.All(existingQuestion => existingQuestion.Id != question.Id))
                    {
                        result.Add(question);
                    }
                }
            }

            return result;
        }

        ContentQuestionDto MapCourseQuestion(ContentQuestion question)
        {
            return new ContentQuestionDto
            {
                Id = question.Id,
                Question = question.Question ?? string.Empty,
                Options = new[] { question.Option1, question.Option2, question.Option3, question.Option4 }
            };
        }

        var topics = rows
            .GroupBy(x => new { x.TopicId, x.TopicTitle, x.TopicPrice, x.TopicDurationMinutes })
            .Select(group => new CourseTopicDto
            {
                Id = group.Key.TopicId,
                Title = group.Key.TopicTitle,
                Price = group.Key.TopicPrice,
                DurationMinutes = group.Key.TopicDurationMinutes,
                HasNotes = topicsWithNotes.Contains(group.Key.TopicId),
                ChapterTitles = chapterRows
                    .Where(chapter => chapter.TopicId == group.Key.TopicId)
                    .Select(chapter => chapter.Title)
                    .ToList(),
                McqCount = questions.Count(question =>
                    (question.TopicId == group.Key.TopicId)
                    || (question.ContentId.HasValue && topicIdByContentId.TryGetValue(question.ContentId.Value, out var contentTopicId) && contentTopicId == group.Key.TopicId)
                    || (question.NoteId.HasValue && topicIdByChapterId.TryGetValue(question.NoteId.Value, out var chapterTopicId) && chapterTopicId == group.Key.TopicId)),
                InterviewQuestionCount = interviewRows.Count(question =>
                    (question.TopicId == group.Key.TopicId)
                    || (question.ContentId.HasValue && topicIdByContentId.TryGetValue(question.ContentId.Value, out var contentTopicId) && contentTopicId == group.Key.TopicId)
                    || (question.NoteId.HasValue && topicIdByChapterId.TryGetValue(question.NoteId.Value, out var chapterTopicId) && chapterTopicId == group.Key.TopicId)),
                PracticeProgramCount = practiceRows.Count(question =>
                    (question.ContentId.HasValue && topicIdByContentId.TryGetValue(question.ContentId.Value, out var contentTopicId) && contentTopicId == group.Key.TopicId)
                    || (question.NoteId.HasValue && topicIdByChapterId.TryGetValue(question.NoteId.Value, out var chapterTopicId) && chapterTopicId == group.Key.TopicId)),
                Contents = group
                    .Where(x => x.ContentId.HasValue)
                    .Select(x => new CourseContentDto
                    {
                        Id = x.ContentId!.Value,
                        Title = x.ContentTitle ?? string.Empty,
                        Slides = x.Slides,
                        VideoName = x.VideoName,
                        Questions = GetQuestionsForCourseContent(x.ContentId.Value, x.TopicId, x.NoteId)
                    })
                    .ToList()
            })
            .ToList();

        var presentation = Presentations.TryGetValue(course.Title, out var known)
            ? known
            : CoursePresentation.Default;

        return new CourseDetailsDto
        {
            Id = course.Id,
            Title = course.Title,
            ShortDescription = presentation.ShortDescription,
            Description = presentation.Description,
            ImageUrl = course.ImagePath ?? presentation.ImageUrl,
            Category = presentation.Category,
            Level = presentation.Level,
            DurationHours = topics.Sum(topic => topic.DurationMinutes ?? 0) / 60m,
            OriginalPrice = topics.Sum(topic => Math.Max(0m, topic.Price)),
            TotalPrice = CalculateDiscountedPrice(topics.Sum(topic => Math.Max(0m, topic.Price)), course.DiscountMode, course.DiscountValue),
            DiscountAmount = Math.Max(0m, topics.Sum(topic => Math.Max(0m, topic.Price)) - CalculateDiscountedPrice(topics.Sum(topic => Math.Max(0m, topic.Price)), course.DiscountMode, course.DiscountValue)),
            DiscountPercentage = GetDiscountPercentage(topics.Sum(topic => Math.Max(0m, topic.Price)), course.DiscountMode, course.DiscountValue),
            Topics = topics
        };
    }

    private static decimal CalculateDiscountedPrice(decimal originalPrice, string? discountMode, decimal? discountValue)
    {
        if (originalPrice <= 0m || !discountValue.HasValue || discountValue.Value < 0m)
            return Math.Max(0m, originalPrice);
        var discounted = discountMode switch
        {
            "percentage" when discountValue.Value <= 100m => originalPrice * (1m - discountValue.Value / 100m),
            "direct" when discountValue.Value <= originalPrice => discountValue.Value,
            _ => originalPrice
        };
        return Math.Round(Math.Clamp(discounted, 0m, originalPrice), 2, MidpointRounding.AwayFromZero);
    }

    private static decimal? GetDiscountPercentage(decimal originalPrice, string? discountMode, decimal? discountValue)
    {
        if (originalPrice <= 0m || !discountValue.HasValue) return null;
        var discounted = CalculateDiscountedPrice(originalPrice, discountMode, discountValue);
        return discounted < originalPrice ? Math.Round((originalPrice - discounted) / originalPrice * 100m, 2) : null;
    }

    private async Task<IReadOnlyDictionary<int, decimal>> GetDurationHoursByCourseIdsAsync(
        IReadOnlyCollection<int> courseIds,
        CancellationToken cancellationToken)
    {
        if (courseIds.Count == 0)
        {
            return new Dictionary<int, decimal>();
        }

        var durations = await (
            from courseTopic in _dbContext.CourseModules.AsNoTracking()
            join topic in _dbContext.Topics.AsNoTracking()
                on courseTopic.TopicId equals topic.Id
            where courseIds.Contains(courseTopic.CourseId)
                  && courseTopic.Flag == 0
                  && topic.Flag == 0
            group topic by courseTopic.CourseId into topicGroup
            select new
            {
                CourseId = topicGroup.Key,
                TotalMinutes = topicGroup.Sum(topic => topic.DurationMinutes ?? 0)
            })
            .ToListAsync(cancellationToken);

        return durations.ToDictionary(
            item => item.CourseId,
            item => item.TotalMinutes / 60m);
    }

    private static CourseDto Map(
        CIITStackLab.Domain.Entities.Course course,
        decimal durationHours)
    {
        var presentation = Presentations.TryGetValue(course.Title, out var known)
            ? known
            : CoursePresentation.Default;

        return new CourseDto
        {
            Id = course.Id,
            Title = course.Title,
            ShortDescription = presentation.ShortDescription,
            Description = presentation.Description,
            ImageUrl = presentation.ImageUrl,
            Category = presentation.Category,
            Level = presentation.Level,
            DurationHours = durationHours,
            IsPublished = course.Flag == 0
        };
    }

    private sealed record CoursePresentation(
        string ShortDescription,
        string Description,
        string ImageUrl,
        string Category,
        string Level,
        int DurationHours)
    {
        public static CoursePresentation Default { get; } = new(
            "Explore this structured CIIT training course.",
            "Course information is loaded from the existing CIIT training database.",
            "https://images.unsplash.com/photo-1515879218367-8466d910aaa4?auto=format&fit=crop&w=1200&q=80",
            "Training",
            "All Levels",
            0);
    }
}
