using CIITStackLab.Application.DTOs;
using CIITStackLab.Application.Interfaces;
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

    public async Task<IReadOnlyList<AdminNoteDto>> GetAdminIndexAsync(
        CancellationToken cancellationToken = default)
    {
        return await (
            from note in _dbContext.TrainingNotes.AsNoTracking()
            join topic in _dbContext.Topics.AsNoTracking()
                on note.TopicId equals topic.Id
            where note.Flag == 0 && topic.Flag == 0
            orderby topic.Title, note.SortOrder, note.Id
            select new AdminNoteDto
            {
                Id = note.Id,
                TopicId = note.TopicId,
                TopicTitle = topic.Title,
                ChapterId = note.ChapterId,
                Title = note.Title,
                HtmlContent = note.HtmlContent,
                SortOrder = note.SortOrder,
                UpdatedAt = note.UpdatedAt,
                CreatedAt = note.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<AdminNoteDto?> GetAdminByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        return await (
            from note in _dbContext.TrainingNotes.AsNoTracking()
            join topic in _dbContext.Topics.AsNoTracking()
                on note.TopicId equals topic.Id
            where note.Id == id && note.Flag == 0 && topic.Flag == 0
            select new AdminNoteDto
            {
                Id = note.Id,
                TopicId = note.TopicId,
                TopicTitle = topic.Title,
                ChapterId = note.ChapterId,
                Title = note.Title,
                HtmlContent = note.HtmlContent,
                SortOrder = note.SortOrder,
                UpdatedAt = note.UpdatedAt,
                CreatedAt = note.CreatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<NoteTopicOptionDto>> GetTopicOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Topics
            .AsNoTracking()
            .Where(topic => topic.Flag == 0)
            .OrderBy(topic => topic.Title)
            .Select(topic => new NoteTopicOptionDto
            {
                TopicId = topic.Id,
                TopicTitle = topic.Title
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<(bool Succeeded, string? Error)> CreateAsync(
        AdminNoteInputDto input,
        CancellationToken cancellationToken = default)
    {
        var error = await ValidateTargetAsync(
            input.TopicId,
            input.ChapterId,
            null,
            cancellationToken);

        if (error is not null)
        {
            return (false, error);
        }

        var note = new Domain.Entities.TrainingNote
        {
            TopicId = input.TopicId,
            ChapterId = input.ChapterId.Trim(),
            Title = input.Title.Trim(),
            HtmlContent = input.HtmlContent.Trim(),
            SortOrder = input.SortOrder,
            Flag = 0,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        _dbContext.TrainingNotes.Add(note);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return (true, null);
    }

    public async Task<(bool Succeeded, string? Error)> UpdateAsync(
        int id,
        AdminNoteInputDto input,
        CancellationToken cancellationToken = default)
    {
        var note = await _dbContext.TrainingNotes
            .SingleOrDefaultAsync(
                x => x.Id == id && x.Flag == 0,
                cancellationToken);

        if (note is null)
        {
            return (false, "The selected note was not found or is inactive.");
        }

        var error = await ValidateTargetAsync(
            input.TopicId,
            input.ChapterId,
            id,
            cancellationToken);

        if (error is not null)
        {
            return (false, error);
        }

        note.TopicId = input.TopicId;
        note.ChapterId = input.ChapterId.Trim();
        note.Title = input.Title.Trim();
        note.HtmlContent = input.HtmlContent.Trim();
        note.SortOrder = input.SortOrder;
        note.UpdatedAt = DateTime.Now;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return (true, null);
    }

    public async Task<(bool Succeeded, string? Error)> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var note = await _dbContext.TrainingNotes
            .SingleOrDefaultAsync(
                x => x.Id == id && x.Flag == 0,
                cancellationToken);

        if (note is null)
        {
            return (false, "The selected note was not found or is inactive.");
        }

        note.Flag = 1;
        note.DeletedAt = DateTime.Now;
        note.UpdatedAt = DateTime.Now;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return (true, null);
    }

    public async Task<TopicNotesDto?> GetReaderAsync(
        int topicId,
        string? chapterId,
        CancellationToken cancellationToken = default)
    {
        var topic = await _dbContext.Topics
            .AsNoTracking()
            .Where(x => x.Id == topicId && x.Flag == 0)
            .Select(x => new
            {
                x.Id,
                x.Title
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (topic is null)
        {
            return null;
        }

        var chapters = await _dbContext.TrainingNotes
            .AsNoTracking()
            .Where(x => x.TopicId == topicId && x.Flag == 0)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .Select(x => new NoteChapterDto
            {
                Id = x.Id,
                ChapterId = x.ChapterId,
                Title = x.Title,
                HtmlContent = x.HtmlContent,
                SortOrder = x.SortOrder
            })
            .ToListAsync(cancellationToken);

        if (chapters.Count == 0)
        {
            return null;
        }

        var chapterIds = chapters.Select(x => x.Id).ToList();
        var mediaRows = await _dbContext.Lessons
            .AsNoTracking()
            .Where(x => x.NoteId.HasValue && chapterIds.Contains(x.NoteId.Value) && x.Flag == 0)
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.NoteId,
                Media = new NoteMediaDto
                {
                    ContentId = x.Id,
                    Title = x.Title,
                    Slides = x.Slides,
                    VideoName = x.VideoName
                }
            })
            .ToListAsync(cancellationToken);

        var mediaByNote = mediaRows
            .GroupBy(x => x.NoteId!.Value)
            .ToDictionary(x => x.Key, x => (IReadOnlyList<NoteMediaDto>)x.Select(y => y.Media).ToList());

        chapters = chapters.Select(chapter => new NoteChapterDto
        {
            Id = chapter.Id,
            ChapterId = chapter.ChapterId,
            Title = chapter.Title,
            HtmlContent = chapter.HtmlContent,
            SortOrder = chapter.SortOrder,
            Media = mediaByNote.TryGetValue(chapter.Id, out var media)
                ? media
                : Array.Empty<NoteMediaDto>()
        }).ToList();

        var current = !string.IsNullOrWhiteSpace(chapterId)
            ? chapters.FirstOrDefault(x => x.ChapterId == chapterId) ?? chapters[0]
            : chapters[0];

        return new TopicNotesDto
        {
            TopicId = topic.Id,
            TopicTitle = topic.Title,
            Chapters = chapters,
            CurrentChapterId = current.ChapterId,
            CurrentChapter = current
        };
    }

    private async Task<string?> ValidateTargetAsync(
        int topicId,
        string chapterId,
        int? excludingId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(chapterId))
        {
            return "Chapter ID is required.";
        }

        var topicExists = await _dbContext.Topics
            .AnyAsync(
                x => x.Id == topicId && x.Flag == 0,
                cancellationToken);

        if (!topicExists)
        {
            return "The selected topic was not found or is inactive.";
        }

        var normalizedChapterId = chapterId.Trim();

        var duplicate = await _dbContext.TrainingNotes
            .AnyAsync(
                x => x.TopicId == topicId &&
                     x.ChapterId == normalizedChapterId &&
                     x.Flag == 0 &&
                     (!excludingId.HasValue || x.Id != excludingId.Value),
                cancellationToken);

        return duplicate
            ? "This Chapter ID already exists for the selected topic."
            : null;
    }
}
