using CIITStackLab.Application.DTOs;
using CIITStackLab.Application.Interfaces;
using CIITStackLab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CIITStackLab.Infrastructure.Services;

public sealed class AdminNotesService : IAdminNotesService
{
    private readonly ApplicationDbContext _dbContext;

    public AdminNotesService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<AdminNotesDto>> GetIndexAsync(
        CancellationToken cancellationToken = default)
    {
        return await BuildQuery()
            .OrderBy(x => x.CourseTitle)
            .ThenBy(x => x.TopicTitle)
            .ThenBy(x => x.Title)
            .Select(x => ToDto(x))
            .ToListAsync(cancellationToken);
    }

    public async Task<AdminNotesDto?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        return await BuildQuery()
            .Where(x => x.Id == id)
            .Select(x => ToDto(x))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<(bool Succeeded, string? Error)> UpdateHtmlContentAsync(
        int id,
        AdminNotesInputDto input,
        CancellationToken cancellationToken = default)
    {
        var lesson = await _dbContext.Lessons
            .SingleOrDefaultAsync(x => x.Id == id && x.Flag == 0, cancellationToken);

        if (lesson is null)
        {
            return (false, "The selected content was not found or is inactive.");
        }

        if (string.IsNullOrWhiteSpace(input.HtmlContent))
        {
            return (false, "HTML content is required.");
        }

        lesson.HtmlContent = input.HtmlContent.Trim();
        lesson.UpdatedAt = DateTime.Now;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    private IQueryable<NotesRow> BuildQuery()
    {
        return
            from lesson in _dbContext.Lessons.AsNoTracking()
            where lesson.TopicId.HasValue && lesson.Flag == 0
            join topic in _dbContext.Topics.AsNoTracking()
                on lesson.TopicId!.Value equals topic.Id
            join courseModule in _dbContext.CourseModules.AsNoTracking()
                on topic.Id equals courseModule.TopicId
            join course in _dbContext.Courses.AsNoTracking()
                on courseModule.CourseId equals course.Id
            where topic.Flag == 0
                  && courseModule.Flag == 0
                  && course.Flag == 0
            select new NotesRow
            {
                Id = lesson.Id,
                CourseId = course.Id,
                CourseTitle = course.Title,
                TopicId = topic.Id,
                TopicTitle = topic.Title,
                Title = lesson.Title,
                HtmlContent = lesson.HtmlContent,
                UpdatedAt = lesson.UpdatedAt,
                CreatedAt = lesson.CreatedAt
            };
    }

    private static AdminNotesDto ToDto(NotesRow row) => new()
    {
        Id = row.Id,
        CourseId = row.CourseId,
        CourseTitle = row.CourseTitle,
        TopicId = row.TopicId,
        TopicTitle = row.TopicTitle,
        Title = row.Title,
        HtmlContent = row.HtmlContent,
        UpdatedAt = row.UpdatedAt,
        CreatedAt = row.CreatedAt
    };

    private sealed class NotesRow
    {
        public int Id { get; init; }
        public int CourseId { get; init; }
        public string CourseTitle { get; init; } = string.Empty;
        public int TopicId { get; init; }
        public string TopicTitle { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string? HtmlContent { get; init; }
        public DateTime? UpdatedAt { get; init; }
        public DateTime? CreatedAt { get; init; }
    }
}
