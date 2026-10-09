using CIITStackLab.Application.DTOs;
using CIITStackLab.Application.Interfaces;
using CIITStackLab.Domain.Entities;
using CIITStackLab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CIITStackLab.Infrastructure.Services;

public sealed class AdminMcqService : IAdminMcqService
{
    private readonly ApplicationDbContext _dbContext;

    public AdminMcqService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AdminMcqIndexDto> GetIndexAsync(CancellationToken cancellationToken = default)
    {
        var active = await _dbContext.ContentQuestions
            .AsNoTracking()
            .Where(x => x.Flag == 0)
            .OrderByDescending(x => x.Id)
            .Select(x => new AdminMcqDto
            {
                Id = x.Id,
                ContentId = x.ContentId,
                ContentTitle = x.Content != null ? x.Content.Title ?? string.Empty : "Unassigned",
                TopicTitle = x.Content != null && x.Content.Topic != null ? x.Content.Topic.Title : "Unassigned",
                Question = x.Question ?? string.Empty,
                Option1 = x.Option1 ?? string.Empty,
                Option2 = x.Option2 ?? string.Empty,
                Option3 = x.Option3 ?? string.Empty,
                Option4 = x.Option4 ?? string.Empty,
                CorrectOptionNumber = x.CorrectOptionNumber ?? 0,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        var archived = await _dbContext.ContentQuestions
            .AsNoTracking()
            .Where(x => x.Flag == 1)
            .OrderByDescending(x => x.Id)
            .Select(x => new AdminArchivedMcqDto
            {
                Id = x.Id,
                ContentTitle = x.Content != null ? x.Content.Title ?? string.Empty : "Unassigned",
                TopicTitle = x.Content != null && x.Content.Topic != null ? x.Content.Topic.Title : "Unassigned",
                Question = x.Question ?? string.Empty,
                DeletedAt = x.DeletedAt
            })
            .ToListAsync(cancellationToken);

        return new AdminMcqIndexDto
        {
            ActiveMcqs = active,
            ArchivedMcqs = archived
        };
    }

    public async Task<AdminMcqDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ContentQuestions
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new AdminMcqDto
            {
                Id = x.Id,
                ContentId = x.ContentId,
                ContentTitle = x.Content != null ? x.Content.Title ?? string.Empty : "Unassigned",
                TopicTitle = x.Content != null && x.Content.Topic != null ? x.Content.Topic.Title : "Unassigned",
                Question = x.Question ?? string.Empty,
                Option1 = x.Option1 ?? string.Empty,
                Option2 = x.Option2 ?? string.Empty,
                Option3 = x.Option3 ?? string.Empty,
                Option4 = x.Option4 ?? string.Empty,
                CorrectOptionNumber = x.CorrectOptionNumber ?? 0,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            })
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AdminMcqContentLookupDto>> GetContentLookupAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Lessons
            .AsNoTracking()
            .Where(x => x.Flag == 0 && x.Topic != null && x.Topic.Flag == 0)
            .OrderBy(x => x.Topic!.Title)
            .ThenBy(x => x.Title)
            .Select(x => new AdminMcqContentLookupDto
            {
                Id = x.Id,
                Title = x.Title ?? string.Empty,
                TopicTitle = x.Topic!.Title
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<(bool Succeeded, string? Error)> CreateAsync(AdminMcqInputDto input, CancellationToken cancellationToken = default)
    {
        var contentExists = await _dbContext.Lessons.AnyAsync(
            x => x.Id == input.ContentId && x.Flag == 0 && x.Topic != null && x.Topic.Flag == 0,
            cancellationToken);

        if (!contentExists)
            return (false, "The selected content is not available.");

        var question = new ContentQuestion
        {
            ContentId = input.ContentId,
            Question = input.Question.Trim(),
            Option1 = input.Option1.Trim(),
            Option2 = input.Option2.Trim(),
            Option3 = input.Option3.Trim(),
            Option4 = input.Option4.Trim(),
            CorrectOptionNumber = input.CorrectOptionNumber,
            Flag = 0,
            CreatedAt = DateTime.Now
        };

        _dbContext.ContentQuestions.Add(question);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<(bool Succeeded, string? Error)> UpdateAsync(int id, AdminMcqInputDto input, CancellationToken cancellationToken = default)
    {
        var question = await _dbContext.ContentQuestions
            .SingleOrDefaultAsync(x => x.Id == id && x.Flag == 0, cancellationToken);

        if (question is null)
            return (false, "The selected MCQ was not found.");

        var contentExists = await _dbContext.Lessons.AnyAsync(
            x => x.Id == input.ContentId && x.Flag == 0 && x.Topic != null && x.Topic.Flag == 0,
            cancellationToken);

        if (!contentExists)
            return (false, "The selected content is not available.");

        question.ContentId = input.ContentId;
        question.Question = input.Question.Trim();
        question.Option1 = input.Option1.Trim();
        question.Option2 = input.Option2.Trim();
        question.Option3 = input.Option3.Trim();
        question.Option4 = input.Option4.Trim();
        question.CorrectOptionNumber = input.CorrectOptionNumber;
        question.UpdatedAt = DateTime.Now;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<(bool Succeeded, string? Error)> SoftDeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var question = await _dbContext.ContentQuestions
            .SingleOrDefaultAsync(x => x.Id == id && x.Flag == 0, cancellationToken);

        if (question is null)
            return (false, "The selected MCQ was not found.");

        question.Flag = 1;
        question.DeletedAt = DateTime.Now;
        question.UpdatedAt = DateTime.Now;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<(bool Succeeded, string? Error)> RestoreAsync(int id, CancellationToken cancellationToken = default)
    {
        var question = await _dbContext.ContentQuestions
            .SingleOrDefaultAsync(x => x.Id == id && x.Flag == 1, cancellationToken);

        if (question is null)
            return (false, "The selected archived MCQ was not found.");

        question.Flag = 0;
        question.DeletedAt = null;
        question.RestoredAt = DateTime.Now;
        question.UpdatedAt = DateTime.Now;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return (true, null);
    }
}
