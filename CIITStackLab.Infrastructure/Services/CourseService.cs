using CIITStackLab.Application.DTOs;
using CIITStackLab.Application.Interfaces;
using CIITStackLab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CIITStackLab.Infrastructure.Services;

public sealed class CourseService : ICourseService
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

        return courses.Select(Map).ToList();
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

        return course is null ? null : Map(course);
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
                ContentId = (int?)content.Id,
                ContentTitle = content.Title,
                Slides = content.Slides,
                VideoName = content.VideoName
            })
            .ToListAsync(cancellationToken);

        var contentIds = rows
            .Where(x => x.ContentId.HasValue)
            .Select(x => x.ContentId!.Value)
            .Distinct()
            .ToList();

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

        var topics = rows
            .GroupBy(x => new { x.TopicId, x.TopicTitle })
            .Select(group => new CourseTopicDto
            {
                Id = group.Key.TopicId,
                Title = group.Key.TopicTitle,
                Contents = group
                    .Where(x => x.ContentId.HasValue)
                    .Select(x => new CourseContentDto
                    {
                        Id = x.ContentId!.Value,
                        Title = x.ContentTitle ?? string.Empty,
                        Slides = x.Slides,
                        VideoName = x.VideoName,
                        Questions = questionsByContentId.TryGetValue(x.ContentId.Value, out var contentQuestions)
                            ? contentQuestions
                            : Array.Empty<ContentQuestionDto>()
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
            ImageUrl = presentation.ImageUrl,
            Category = presentation.Category,
            Level = presentation.Level,
            DurationHours = presentation.DurationHours,
            Topics = topics
        };
    }

    private static CourseDto Map(CIITStackLab.Domain.Entities.Course course)
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
            DurationHours = presentation.DurationHours,
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
