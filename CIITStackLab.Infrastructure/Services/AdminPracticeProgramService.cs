using CIITStackLab.Application.DTOs;
using CIITStackLab.Application.Interfaces;
using CIITStackLab.Domain.Entities;
using CIITStackLab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CIITStackLab.Infrastructure.Services;

public sealed class AdminPracticeProgramService : IAdminPracticeProgramService
{
    private readonly ApplicationDbContext _dbContext;

    public AdminPracticeProgramService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AdminPracticeProgramIndexDto> GetIndexAsync(CancellationToken cancellationToken = default)
    {
        var activePrograms = await _dbContext.ContentProgramQuestions
            .AsNoTracking()
            .Where(x => x.Flag == 0)
            .OrderByDescending(x => x.Id)
            .Select(x => new AdminPracticeProgramDto
            {
                Id = x.Id,
                ContentId = x.ContentId,
                ContentTitle = x.Content != null ? x.Content.Title ?? string.Empty : "Unassigned",
                TopicTitle = x.Content != null && x.Content.Topic != null ? x.Content.Topic.Title : "Unassigned",
                QuestionTitle = x.QuestionTitle,
                QuestionDescription = x.QuestionDescription,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        var activeAnswers = await _dbContext.ContentProgramAnswers
            .AsNoTracking()
            .Where(x => x.Flag == 0 && x.ProgramQuestion != null && x.ProgramQuestion.Flag == 0)
            .Select(x => new AdminPracticeProgramAnswerDto
            {
                Id = x.Id,
                ProgramQuestionId = x.ProgramQuestionId,
                Answer = x.Answer,
                Description = x.Description,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        var answersByProgram = activeAnswers
            .Where(x => x.ProgramQuestionId.HasValue)
            .GroupBy(x => x.ProgramQuestionId!.Value)
            .ToDictionary(x => x.Key, x => (IReadOnlyList<AdminPracticeProgramAnswerDto>)x.OrderByDescending(a => a.Id).ToList());

        foreach (var program in activePrograms)
        {
            program.Answers = answersByProgram.TryGetValue(program.Id, out var answers)
                ? answers
                : Array.Empty<AdminPracticeProgramAnswerDto>();
        }

        var archivedPrograms = await _dbContext.ContentProgramQuestions
            .AsNoTracking()
            .Where(x => x.Flag == 1)
            .OrderByDescending(x => x.Id)
            .Select(x => new AdminArchivedPracticeProgramDto
            {
                Id = x.Id,
                ContentTitle = x.Content != null ? x.Content.Title ?? string.Empty : "Unassigned",
                TopicTitle = x.Content != null && x.Content.Topic != null ? x.Content.Topic.Title : "Unassigned",
                QuestionTitle = x.QuestionTitle,
                DeletedAt = x.DeletedAt
            })
            .ToListAsync(cancellationToken);

        var archivedAnswers = await _dbContext.ContentProgramAnswers
            .AsNoTracking()
            .Where(x => x.Flag == 1)
            .OrderByDescending(x => x.Id)
            .Select(x => new AdminArchivedPracticeProgramAnswerDto
            {
                Id = x.Id,
                ProgramQuestionId = x.ProgramQuestionId,
                QuestionTitle = x.ProgramQuestion != null ? x.ProgramQuestion.QuestionTitle : "Archived program",
                Answer = x.Answer,
                DeletedAt = x.DeletedAt
            })
            .ToListAsync(cancellationToken);

        return new AdminPracticeProgramIndexDto
        {
            ActivePrograms = activePrograms,
            ArchivedPrograms = archivedPrograms,
            ArchivedAnswers = archivedAnswers
        };
    }

    public async Task<AdminPracticeProgramDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var program = await _dbContext.ContentProgramQuestions
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new AdminPracticeProgramDto
            {
                Id = x.Id,
                ContentId = x.ContentId,
                ContentTitle = x.Content != null ? x.Content.Title ?? string.Empty : "Unassigned",
                TopicTitle = x.Content != null && x.Content.Topic != null ? x.Content.Topic.Title : "Unassigned",
                QuestionTitle = x.QuestionTitle,
                QuestionDescription = x.QuestionDescription,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (program is null)
            return null;

        program.Answers = await _dbContext.ContentProgramAnswers
            .AsNoTracking()
            .Where(x => x.Flag == 0 && x.ProgramQuestionId == id)
            .OrderByDescending(x => x.Id)
            .Select(x => new AdminPracticeProgramAnswerDto
            {
                Id = x.Id,
                ProgramQuestionId = x.ProgramQuestionId,
                Answer = x.Answer,
                Description = x.Description,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        return program;
    }

    public async Task<AdminPracticeProgramAnswerDto?> GetAnswerByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ContentProgramAnswers
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new AdminPracticeProgramAnswerDto
            {
                Id = x.Id,
                ProgramQuestionId = x.ProgramQuestionId,
                Answer = x.Answer,
                Description = x.Description,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            })
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AdminPracticeProgramContentLookupDto>> GetContentLookupAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Lessons
            .AsNoTracking()
            .Where(x => x.Flag == 0 && x.Topic != null && x.Topic.Flag == 0)
            .OrderBy(x => x.Topic!.Title)
            .ThenBy(x => x.Title)
            .Select(x => new AdminPracticeProgramContentLookupDto
            {
                Id = x.Id,
                Title = x.Title ?? string.Empty,
                TopicTitle = x.Topic!.Title
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AdminPracticeProgramLookupDto>> GetProgramLookupAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.ContentProgramQuestions
            .AsNoTracking()
            .Where(x => x.Flag == 0)
            .OrderBy(x => x.QuestionTitle)
            .Select(x => new AdminPracticeProgramLookupDto
            {
                Id = x.Id,
                QuestionTitle = x.QuestionTitle
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<(bool Succeeded, string? Error)> CreateAsync(AdminPracticeProgramInputDto input, CancellationToken cancellationToken = default)
    {
        if (!await ContentExistsAsync(input.ContentId, cancellationToken))
            return (false, "The selected content is not available.");

        var program = new ContentProgramQuestion
        {
            ContentId = input.ContentId,
            QuestionTitle = input.QuestionTitle.Trim(),
            QuestionDescription = input.QuestionDescription.Trim(),
            Flag = 0,
            CreatedAt = DateTime.Now
        };

        _dbContext.ContentProgramQuestions.Add(program);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<(bool Succeeded, string? Error)> UpdateAsync(int id, AdminPracticeProgramInputDto input, CancellationToken cancellationToken = default)
    {
        var program = await _dbContext.ContentProgramQuestions
            .SingleOrDefaultAsync(x => x.Id == id && x.Flag == 0, cancellationToken);

        if (program is null)
            return (false, "The selected practice program was not found.");

        if (!await ContentExistsAsync(input.ContentId, cancellationToken))
            return (false, "The selected content is not available.");

        program.ContentId = input.ContentId;
        program.QuestionTitle = input.QuestionTitle.Trim();
        program.QuestionDescription = input.QuestionDescription.Trim();
        program.UpdatedAt = DateTime.Now;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<(bool Succeeded, string? Error)> SoftDeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var program = await _dbContext.ContentProgramQuestions
            .SingleOrDefaultAsync(x => x.Id == id && x.Flag == 0, cancellationToken);

        if (program is null)
            return (false, "The selected practice program was not found.");

        program.Flag = 1;
        program.DeletedAt = DateTime.Now;
        program.UpdatedAt = DateTime.Now;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<(bool Succeeded, string? Error)> RestoreAsync(int id, CancellationToken cancellationToken = default)
    {
        var program = await _dbContext.ContentProgramQuestions
            .SingleOrDefaultAsync(x => x.Id == id && x.Flag == 1, cancellationToken);

        if (program is null)
            return (false, "The selected archived practice program was not found.");

        if (!await ContentExistsAsync(program.ContentId, cancellationToken))
            return (false, "The original content is no longer available, so this program cannot be restored.");

        program.Flag = 0;
        program.DeletedAt = null;
        program.RestoredAt = DateTime.Now;
        program.UpdatedAt = DateTime.Now;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<(bool Succeeded, string? Error)> CreateAnswerAsync(AdminPracticeProgramAnswerInputDto input, CancellationToken cancellationToken = default)
    {
        if (!await ActiveProgramExistsAsync(input.ProgramQuestionId, cancellationToken))
            return (false, "The selected practice program is not available.");

        var answer = new ContentProgramAnswer
        {
            ProgramQuestionId = input.ProgramQuestionId,
            Answer = input.Answer.Trim(),
            Description = input.Description.Trim(),
            Flag = 0,
            CreatedAt = DateTime.Now
        };

        _dbContext.ContentProgramAnswers.Add(answer);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<(bool Succeeded, string? Error)> UpdateAnswerAsync(int id, AdminPracticeProgramAnswerInputDto input, CancellationToken cancellationToken = default)
    {
        var answer = await _dbContext.ContentProgramAnswers
            .SingleOrDefaultAsync(x => x.Id == id && x.Flag == 0, cancellationToken);

        if (answer is null)
            return (false, "The selected practice program answer was not found.");

        if (!await ActiveProgramExistsAsync(input.ProgramQuestionId, cancellationToken))
            return (false, "The selected practice program is not available.");

        answer.ProgramQuestionId = input.ProgramQuestionId;
        answer.Answer = input.Answer.Trim();
        answer.Description = input.Description.Trim();
        answer.UpdatedAt = DateTime.Now;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<(bool Succeeded, string? Error)> SoftDeleteAnswerAsync(int id, CancellationToken cancellationToken = default)
    {
        var answer = await _dbContext.ContentProgramAnswers
            .SingleOrDefaultAsync(x => x.Id == id && x.Flag == 0, cancellationToken);

        if (answer is null)
            return (false, "The selected practice program answer was not found.");

        answer.Flag = 1;
        answer.DeletedAt = DateTime.Now;
        answer.UpdatedAt = DateTime.Now;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<(bool Succeeded, string? Error)> RestoreAnswerAsync(int id, CancellationToken cancellationToken = default)
    {
        var answer = await _dbContext.ContentProgramAnswers
            .SingleOrDefaultAsync(x => x.Id == id && x.Flag == 1, cancellationToken);

        if (answer is null)
            return (false, "The selected archived practice program answer was not found.");

        if (!answer.ProgramQuestionId.HasValue || !await ActiveProgramExistsAsync(answer.ProgramQuestionId.Value, cancellationToken))
            return (false, "The parent practice program is not available, so this answer cannot be restored.");

        answer.Flag = 0;
        answer.DeletedAt = null;
        answer.RestoredAt = DateTime.Now;
        answer.UpdatedAt = DateTime.Now;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    private Task<bool> ContentExistsAsync(int contentId, CancellationToken cancellationToken)
    {
        return _dbContext.Lessons.AnyAsync(
            x => x.Id == contentId && x.Flag == 0 && x.Topic != null && x.Topic.Flag == 0,
            cancellationToken);
    }

    private Task<bool> ActiveProgramExistsAsync(int programQuestionId, CancellationToken cancellationToken)
    {
        return _dbContext.ContentProgramQuestions.AnyAsync(
            x => x.Id == programQuestionId && x.Flag == 0,
            cancellationToken);
    }
}
