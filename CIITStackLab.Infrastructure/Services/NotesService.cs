using CIITStackLab.Application.DTOs;
using CIITStackLab.Application.Interfaces;
using CIITStackLab.Domain.Entities;
using CIITStackLab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CIITStackLab.Infrastructure.Services;

public sealed class NotesService : INotesService
{
    private readonly ApplicationDbContext _dbContext;

    public NotesService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<AdminNoteDto>> GetAdminIndexAsync(CancellationToken cancellationToken = default)
    {
        return await (
            from content in _dbContext.Lessons.AsNoTracking()
            join topic in _dbContext.Topics.AsNoTracking() on content.TopicId equals (int?)topic.Id
            where content.Flag == 0 && topic.Flag == 0
            orderby topic.Title, content.Id
            select new AdminNoteDto
            {
                Id = content.Id,
                TopicId = topic.Id,
                TopicTitle = topic.Title,
                ChapterId = content.Id.ToString(),
                Title = content.Title,
                HtmlContent = content.HtmlContent ?? string.Empty,
                SortOrder = content.Id,
                UpdatedAt = content.UpdatedAt,
                CreatedAt = content.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<AdminNoteDto?> GetAdminByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await (
            from content in _dbContext.Lessons.AsNoTracking()
            join topic in _dbContext.Topics.AsNoTracking() on content.TopicId equals (int?)topic.Id
            where content.Id == id && content.Flag == 0 && topic.Flag == 0
            select new AdminNoteDto
            {
                Id = content.Id,
                TopicId = topic.Id,
                TopicTitle = topic.Title,
                ChapterId = content.Id.ToString(),
                Title = content.Title,
                HtmlContent = content.HtmlContent ?? string.Empty,
                SortOrder = content.Id,
                UpdatedAt = content.UpdatedAt,
                CreatedAt = content.CreatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<NoteTopicOptionDto>> GetTopicOptionsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Topics.AsNoTracking()
            .Where(topic => topic.Flag == 0)
            .OrderBy(topic => topic.Title)
            .Select(topic => new NoteTopicOptionDto { TopicId = topic.Id, TopicTitle = topic.Title })
            .ToListAsync(cancellationToken);
    }

    public async Task<(bool Succeeded, string? Error)> CreateAsync(AdminNoteInputDto input, CancellationToken cancellationToken = default)
    {
        var error = await ValidateTargetAsync(input.TopicId, cancellationToken);
        if (error is not null) return (false, error);

        var now = DateTime.Now;
        _dbContext.Lessons.Add(new Lesson
        {
            TopicId = input.TopicId,
            Title = input.Title.Trim(),
            HtmlContent = input.HtmlContent.Trim(),
            Flag = 0,
            CreatedAt = now,
            UpdatedAt = now
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<(bool Succeeded, string? Error)> UpdateAsync(int id, AdminNoteInputDto input, CancellationToken cancellationToken = default)
    {
        var content = await _dbContext.Lessons.SingleOrDefaultAsync(x => x.Id == id && x.Flag == 0, cancellationToken);
        if (content is null) return (false, "The selected content was not found or is inactive.");

        var error = await ValidateTargetAsync(input.TopicId, cancellationToken);
        if (error is not null) return (false, error);

        content.TopicId = input.TopicId;
        content.Title = input.Title.Trim();
        content.HtmlContent = input.HtmlContent.Trim();
        content.UpdatedAt = DateTime.Now;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<(bool Succeeded, string? Error)> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var content = await _dbContext.Lessons.SingleOrDefaultAsync(x => x.Id == id && x.Flag == 0, cancellationToken);
        if (content is null) return (false, "The selected content was not found or is inactive.");

        content.Flag = 1;
        content.DeletedAt = DateTime.Now;
        content.UpdatedAt = DateTime.Now;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<TopicNotesDto?> GetReaderAsync(int topicId, string? chapterId, CancellationToken cancellationToken = default)
    {
        var topic = await _dbContext.Topics.AsNoTracking()
            .Where(x => x.Id == topicId && x.Flag == 0)
            .Select(x => new { x.Id, x.Title })
            .FirstOrDefaultAsync(cancellationToken);
        if (topic is null) return null;

        var chapters = await _dbContext.Lessons.AsNoTracking()
            .Where(x => x.TopicId == topicId && x.Flag == 0)
            .OrderBy(x => x.Id)
            .Select(x => new NoteChapterDto
            {
                Id = x.Id,
                ChapterId = x.Id.ToString(),
                Title = x.Title,
                HtmlContent = x.HtmlContent ?? string.Empty,
                SortOrder = x.Id,
                Media = new[]
                {
                    new NoteMediaDto
                    {
                        ContentId = x.Id,
                        Title = x.Title,
                        Slides = x.Slides,
                        VideoName = x.VideoName
                    }
                }
            })
            .ToListAsync(cancellationToken);
        if (chapters.Count == 0) return null;

        var current = int.TryParse(chapterId, out var contentId)
            ? chapters.FirstOrDefault(x => x.Id == contentId) ?? chapters[0]
            : chapters[0];

        return new TopicNotesDto
        {
            TopicId = topic.Id,
            TopicTitle = topic.Title,
            Chapters = chapters,
            CurrentChapterId = current.Id.ToString(),
            CurrentChapter = current
        };
    }

    private async Task<string?> ValidateTargetAsync(int topicId, CancellationToken cancellationToken)
    {
        var topicExists = await _dbContext.Topics
            .AnyAsync(x => x.Id == topicId && x.Flag == 0, cancellationToken);
        return topicExists ? null : "The selected topic was not found or is inactive.";
    }
}
