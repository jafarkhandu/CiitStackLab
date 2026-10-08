using CIITStackLab.Application.DTOs;
using CIITStackLab.Application.Interfaces;
using CIITStackLab.Domain.Entities;
using CIITStackLab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CIITStackLab.Infrastructure.Services;

public sealed class AdminContentService : IAdminContentService
{
    private readonly ApplicationDbContext _dbContext;

    public AdminContentService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AdminContentIndexDto> GetIndexAsync(CancellationToken cancellationToken = default)
    {
        var active = await BuildContentQuery(0).OrderByDescending(x => x.Id).ToListAsync(cancellationToken);
        var archived = await BuildArchivedQuery().OrderByDescending(x => x.Id).ToListAsync(cancellationToken);

        return new AdminContentIndexDto
        {
            ActiveContents = active.Select(ToContentDto).ToList(),
            ArchivedContents = archived.Select(ToArchivedDto).ToList()
        };
    }

    public async Task<AdminContentDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var row = await BuildContentQuery(null).Where(x => x.Id == id).FirstOrDefaultAsync(cancellationToken);
        return row is null ? null : ToContentDto(row);
    }

    public async Task<IReadOnlyList<AdminLookupDto>> GetCoursesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Courses
            .AsNoTracking()
            .Where(x => x.Flag == 0)
            .OrderBy(x => x.Title)
            .Select(x => new AdminLookupDto { Id = x.Id, Title = x.Title })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AdminLookupDto>> GetTopicsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Topics.AsNoTracking()
            .Where(x => x.Flag == 0).OrderBy(x => x.Title)
            .Select(x => new AdminLookupDto { Id = x.Id, Title = x.Title })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AdminNoteLookupDto>> GetNotesForTopicAsync(int topicId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.TrainingNotes.AsNoTracking()
            .Where(x => x.TopicId == topicId && x.Flag == 0)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Id)
            .Select(x => new AdminNoteLookupDto { Id = x.Id, ChapterId = x.ChapterId, Title = x.Title, SortOrder = x.SortOrder })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AdminLookupDto>> GetTopicsForCourseAsync(int courseId, CancellationToken cancellationToken = default)
    {
        return await (
            from cm in _dbContext.CourseModules.AsNoTracking()
            join t in _dbContext.Topics.AsNoTracking() on cm.TopicId equals t.Id
            where cm.CourseId == courseId && cm.Flag == 0 && t.Flag == 0
            orderby t.Title
            select new AdminLookupDto { Id = t.Id, Title = t.Title }
        ).Distinct().ToListAsync(cancellationToken);
    }

    public async Task<(bool Succeeded, string? Error)> CreateAsync(AdminContentInputDto input, CancellationToken cancellationToken = default)
    {
        var validationError = await ValidateInputAsync(input, cancellationToken);
        if (validationError is not null) return (false, validationError);

        var now = DateTime.Now;
        _dbContext.Lessons.Add(new Lesson
        {
            TopicId = input.TopicId,
            NoteId = input.NoteId,
            Title = input.Title.Trim(),
            Slides = NormalizeOptional(input.Slides),
            VideoName = NormalizeOptional(input.VideoName),
            Flag = 0,
            CreatedAt = now
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<(bool Succeeded, string? Error)> UpdateAsync(int id, AdminContentInputDto input, CancellationToken cancellationToken = default)
    {
        var lesson = await _dbContext.Lessons.SingleOrDefaultAsync(x => x.Id == id && x.Flag == 0, cancellationToken);
        if (lesson is null) return (false, "The selected content was not found.");

        var validationError = await ValidateInputAsync(input, cancellationToken);
        if (validationError is not null) return (false, validationError);

        lesson.TopicId = input.TopicId;
        lesson.NoteId = input.NoteId;
        lesson.Title = input.Title.Trim();
        lesson.Slides = NormalizeOptional(input.Slides);
        lesson.VideoName = NormalizeOptional(input.VideoName);
        lesson.UpdatedAt = DateTime.Now;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<(bool Succeeded, string? Error)> SoftDeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var lesson = await _dbContext.Lessons.SingleOrDefaultAsync(x => x.Id == id && x.Flag == 0, cancellationToken);
        if (lesson is null) return (false, "The selected content was not found.");

        var now = DateTime.Now;
        lesson.Flag = 1;
        lesson.DeletedAt = now;
        lesson.UpdatedAt = now;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<(bool Succeeded, string? Error)> RestoreAsync(int id, CancellationToken cancellationToken = default)
    {
        var lesson = await _dbContext.Lessons.SingleOrDefaultAsync(x => x.Id == id && x.Flag == 1, cancellationToken);
        if (lesson is null) return (false, "The selected archived content was not found.");

        var now = DateTime.Now;
        lesson.Flag = 0;
        lesson.DeletedAt = null;
        lesson.RestoredAt = now;
        lesson.UpdatedAt = now;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    private async Task<string?> ValidateInputAsync(AdminContentInputDto input, CancellationToken cancellationToken)
    {
        input.Title = input.Title?.Trim() ?? string.Empty;
        input.VideoName = NormalizeOptional(input.VideoName);
        input.Slides = NormalizeOptional(input.Slides);

        if (string.IsNullOrWhiteSpace(input.Title)) return "Content name is required.";

        var topicExists = await _dbContext.Topics.AsNoTracking()
            .AnyAsync(x => x.Id == input.TopicId && x.Flag == 0, cancellationToken);
        if (!topicExists) return "The selected topic was not found or is inactive.";

        var noteExists = await _dbContext.TrainingNotes.AsNoTracking()
            .AnyAsync(x => x.Id == input.NoteId && x.TopicId == input.TopicId && x.Flag == 0, cancellationToken);
        return noteExists ? null : "The selected note chapter was not found for this topic.";
    }

    private IQueryable<ContentRow> BuildContentQuery(int? flag)
    {
        var query =
            from l in _dbContext.Lessons.AsNoTracking()
            where l.TopicId.HasValue
            join t in _dbContext.Topics.AsNoTracking() on l.TopicId!.Value equals t.Id
            join note in _dbContext.TrainingNotes.AsNoTracking() on l.NoteId equals (int?)note.Id into noteGroup
            from note in noteGroup.DefaultIfEmpty()
            join cm in _dbContext.CourseModules.AsNoTracking() on t.Id equals cm.TopicId
            join c in _dbContext.Courses.AsNoTracking() on cm.CourseId equals c.Id
            where t.Flag == 0 && cm.Flag == 0 && c.Flag == 0 && (flag == null || l.Flag == flag)
            select new ContentRow
            {
                Id = l.Id,
                CourseId = c.Id,
                CourseTitle = c.Title,
                TopicId = t.Id,
                NoteId = l.NoteId,
                NoteChapterId = note.ChapterId,
                NoteTitle = note.Title,
                TopicTitle = t.Title,
                Title = l.Title,
                Slides = l.Slides,
                VideoName = l.VideoName,
                Flag = l.Flag ?? 0,
                CreatedAt = l.CreatedAt,
                UpdatedAt = l.UpdatedAt,
                DeletedAt = l.DeletedAt,
                RestoredAt = l.RestoredAt
            };

        return query.Distinct();
    }

    private IQueryable<ArchivedContentRow> BuildArchivedQuery()
    {
        var query =
            from l in _dbContext.Lessons.AsNoTracking()
            where l.TopicId.HasValue
            join t in _dbContext.Topics.AsNoTracking() on l.TopicId!.Value equals t.Id
            join cm in _dbContext.CourseModules.AsNoTracking() on t.Id equals cm.TopicId
            join c in _dbContext.Courses.AsNoTracking() on cm.CourseId equals c.Id
            where l.Flag == 1 && t.Flag == 0 && cm.Flag == 0 && c.Flag == 0
            select new ArchivedContentRow
            {
                Id = l.Id,
                CourseId = c.Id,
                CourseTitle = c.Title,
                TopicId = t.Id,
                TopicTitle = t.Title,
                Title = l.Title,
                DeletedAt = l.DeletedAt
            };

        return query.Distinct();
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static AdminContentDto ToContentDto(ContentRow row) => new()
    {
        Id = row.Id,
        CourseId = row.CourseId,
        CourseTitle = row.CourseTitle,
        TopicId = row.TopicId,
        NoteId = row.NoteId,
        NoteChapterId = row.NoteChapterId,
        NoteTitle = row.NoteTitle,
        TopicTitle = row.TopicTitle,
        Title = row.Title,
        Slides = row.Slides,
        VideoName = row.VideoName,
        Flag = row.Flag,
        CreatedAt = row.CreatedAt,
        UpdatedAt = row.UpdatedAt,
        DeletedAt = row.DeletedAt,
        RestoredAt = row.RestoredAt
    };

    private static AdminArchivedContentDto ToArchivedDto(ArchivedContentRow row) => new()
    {
        Id = row.Id,
        CourseId = row.CourseId,
        CourseTitle = row.CourseTitle,
        TopicId = row.TopicId,
        TopicTitle = row.TopicTitle,
        Title = row.Title,
        DeletedAt = row.DeletedAt
    };

    private sealed class ContentRow
    {
        public int Id { get; init; }
        public int CourseId { get; init; }
        public string CourseTitle { get; init; } = string.Empty;
        public int TopicId { get; init; }
        public int? NoteId { get; init; }
        public string NoteChapterId { get; init; } = string.Empty;
        public string NoteTitle { get; init; } = string.Empty;
        public string TopicTitle { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string? Slides { get; init; }
        public string? VideoName { get; init; }
        public int Flag { get; init; }
        public DateTime? CreatedAt { get; init; }
        public DateTime? UpdatedAt { get; init; }
        public DateTime? DeletedAt { get; init; }
        public DateTime? RestoredAt { get; init; }
    }

    private sealed class ArchivedContentRow
    {
        public int Id { get; init; }
        public int CourseId { get; init; }
        public string CourseTitle { get; init; } = string.Empty;
        public int TopicId { get; init; }
        public string TopicTitle { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public DateTime? DeletedAt { get; init; }
    }
}
