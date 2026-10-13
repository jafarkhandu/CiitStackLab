using CIITStackLab.Application.DTOs;
using CIITStackLab.Application.Interfaces;
using CIITStackLab.Domain.Entities;
using CIITStackLab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CIITStackLab.Infrastructure.Services;

public sealed class AdminInterviewQuestionService : IAdminInterviewQuestionService
{
    private readonly ApplicationDbContext _dbContext;

    public AdminInterviewQuestionService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AdminInterviewQuestionIndexDto> GetIndexAsync(CancellationToken cancellationToken = default)
    {
        var active = await _dbContext.ContentInterviewQuestions
            .AsNoTracking()
            .Where(x => x.Flag == 0)
            .OrderByDescending(x => x.Id)
            .Select(x => new AdminInterviewQuestionDto
            {
                Id = x.Id,
                ContentId = x.ContentId,
                TopicId = x.TopicId,
                NoteId = x.NoteId ?? (x.Content != null ? x.Content.NoteId : null),
                ChapterTitle = x.Note != null
                    ? x.Note.Topic.Title + " / " + x.Note.ChapterId + " — " + x.Note.Title
                    : x.Content != null && x.Content.Note != null
                        ? x.Content.Note.Topic.Title + " / " + x.Content.Note.ChapterId + " — " + x.Content.Note.Title
                        : x.Topic != null
                            ? x.Topic.Title + " / legacy topic-level assessment"
                            : "Unassigned",
                ContentTitle = x.Content != null ? x.Content.Title ?? string.Empty : "Unassigned",
                TopicTitle = x.Note != null ? x.Note.Topic.Title : x.Topic != null ? x.Topic.Title : x.Content != null && x.Content.Topic != null ? x.Content.Topic.Title : "Unassigned",
                Question = x.Question ?? string.Empty,
                Answer = x.Answer ?? string.Empty,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        var archived = await _dbContext.ContentInterviewQuestions
            .AsNoTracking()
            .Where(x => x.Flag == 1)
            .OrderByDescending(x => x.Id)
            .Select(x => new AdminArchivedInterviewQuestionDto
            {
                Id = x.Id,
                NoteId = x.NoteId ?? (x.Content != null ? x.Content.NoteId : null),
                ChapterTitle = x.Note != null
                    ? x.Note.Topic.Title + " / " + x.Note.ChapterId + " — " + x.Note.Title
                    : x.Content != null && x.Content.Note != null
                        ? x.Content.Note.Topic.Title + " / " + x.Content.Note.ChapterId + " — " + x.Content.Note.Title
                        : x.Topic != null
                            ? x.Topic.Title + " / legacy topic-level assessment"
                            : "Unassigned",
                ContentTitle = x.Content != null ? x.Content.Title ?? string.Empty : "Unassigned",
                TopicTitle = x.Note != null ? x.Note.Topic.Title : x.Content != null && x.Content.Topic != null ? x.Content.Topic.Title : x.Topic != null ? x.Topic.Title : "Unassigned",
                Question = x.Question ?? string.Empty,
                DeletedAt = x.DeletedAt
            })
            .ToListAsync(cancellationToken);

        return new AdminInterviewQuestionIndexDto
        {
            ActiveQuestions = active,
            ArchivedQuestions = archived
        };
    }

    public async Task<AdminInterviewQuestionDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ContentInterviewQuestions
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new AdminInterviewQuestionDto
            {
                Id = x.Id,
                ContentId = x.ContentId,
                TopicId = x.TopicId,
                NoteId = x.NoteId ?? (x.Content != null ? x.Content.NoteId : null),
                ChapterTitle = x.Note != null
                    ? x.Note.Topic.Title + " / " + x.Note.ChapterId + " — " + x.Note.Title
                    : x.Content != null && x.Content.Note != null
                        ? x.Content.Note.Topic.Title + " / " + x.Content.Note.ChapterId + " — " + x.Content.Note.Title
                        : x.Topic != null
                            ? x.Topic.Title + " / legacy topic-level assessment"
                            : "Unassigned",
                ContentTitle = x.Content != null ? x.Content.Title ?? string.Empty : "Unassigned",
                TopicTitle = x.Note != null ? x.Note.Topic.Title : x.Topic != null ? x.Topic.Title : x.Content != null && x.Content.Topic != null ? x.Content.Topic.Title : "Unassigned",
                Question = x.Question ?? string.Empty,
                Answer = x.Answer ?? string.Empty,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            })
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AdminAssessmentChapterLookupDto>> GetAssessmentChapterLookupAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.TrainingNotes
            .AsNoTracking()
            .Where(note => note.Flag == 0 && note.Topic.Flag == 0)
            .OrderBy(note => note.Topic.Title)
            .ThenBy(note => note.SortOrder)
            .ThenBy(note => note.Id)
            .Select(note => new AdminAssessmentChapterLookupDto
            {
                NoteId = note.Id,
                TopicId = note.TopicId,
                TopicTitle = note.Topic.Title,
                ChapterId = note.ChapterId,
                ChapterTitle = note.Title
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AdminInterviewQuestionContentLookupDto>> GetContentLookupAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Lessons
            .AsNoTracking()
            .Where(x => x.Flag == 0 && x.Topic != null && x.Topic.Flag == 0)
            .OrderBy(x => x.Topic!.Title)
            .ThenBy(x => x.Title)
            .Select(x => new AdminInterviewQuestionContentLookupDto
            {
                Id = x.Id,
                TopicId = x.TopicId,
                Title = x.Title ?? string.Empty,
                TopicTitle = x.Topic!.Title
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<(bool Succeeded, string? Error)> CreateAsync(
        AdminInterviewQuestionInputDto input,
        CancellationToken cancellationToken = default)
    {
        var chapterExists = await _dbContext.TrainingNotes.AnyAsync(
            note => note.Id == input.NoteId && note.Flag == 0 && note.Topic.Flag == 0,
            cancellationToken);

        if (!chapterExists)
        {
            return (false, "The selected Notes chapter is not available.");
        }

        var question = new ContentInterviewQuestion
        {
            NoteId = input.NoteId,
            ContentId = null,
            TopicId = null,
            Question = input.Question.Trim(),
            Answer = input.Answer.Trim(),
            Flag = 0,
            CreatedAt = DateTime.Now
        };

        _dbContext.ContentInterviewQuestions.Add(question);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<(bool Succeeded, string? Error)> UpdateAsync(
        int id,
        AdminInterviewQuestionInputDto input,
        CancellationToken cancellationToken = default)
    {
        var question = await _dbContext.ContentInterviewQuestions
            .SingleOrDefaultAsync(item => item.Id == id && item.Flag == 0, cancellationToken);

        if (question is null)
        {
            return (false, "The selected interview question was not found.");
        }

        var chapterExists = await _dbContext.TrainingNotes.AnyAsync(
            note => note.Id == input.NoteId && note.Flag == 0 && note.Topic.Flag == 0,
            cancellationToken);

        if (!chapterExists)
        {
            return (false, "The selected Notes chapter is not available.");
        }

        question.NoteId = input.NoteId;
        question.ContentId = null;
        question.TopicId = null;
        question.Question = input.Question.Trim();
        question.Answer = input.Answer.Trim();
        question.UpdatedAt = DateTime.Now;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<(bool Succeeded, string? Error)> SoftDeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var question = await _dbContext.ContentInterviewQuestions
            .SingleOrDefaultAsync(x => x.Id == id && x.Flag == 0, cancellationToken);

        if (question is null)
            return (false, "The selected interview question was not found.");

        question.Flag = 1;
        question.DeletedAt = DateTime.Now;
        question.UpdatedAt = DateTime.Now;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<(bool Succeeded, string? Error)> RestoreAsync(int id, CancellationToken cancellationToken = default)
    {
        var question = await _dbContext.ContentInterviewQuestions
            .SingleOrDefaultAsync(x => x.Id == id && x.Flag == 1, cancellationToken);

        if (question is null)
            return (false, "The selected archived interview question was not found.");

        question.Flag = 0;
        question.DeletedAt = null;
        question.RestoredAt = DateTime.Now;
        question.UpdatedAt = DateTime.Now;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return (true, null);
    }
}
