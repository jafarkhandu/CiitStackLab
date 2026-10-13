using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using CIITStackLab.Application.DTOs;
using CIITStackLab.Application.Interfaces;
using CIITStackLab.Domain.Entities;
using CIITStackLab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CIITStackLab.Infrastructure.Services;

public sealed class InterviewQuestionImportService : IInterviewQuestionImportService
{
    private static readonly TimeSpan PreviewLifetime = TimeSpan.FromMinutes(20);

    private static readonly Regex WhitespacePattern = new(
        @"\s+",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly ApplicationDbContext _dbContext;
    private readonly IMemoryCache _cache;
    public InterviewQuestionImportService(
        ApplicationDbContext dbContext,
        IMemoryCache cache)
    {
        _dbContext = dbContext;
        _cache = cache;
    }

    public async Task<IReadOnlyList<InterviewQuestionTopicLookupDto>> GetTopicLookupAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Topics.AsNoTracking()
            .Where(topic => topic.Flag == 0)
            .OrderBy(topic => topic.Title)
            .Select(topic => new InterviewQuestionTopicLookupDto
            {
                Id = topic.Id,
                Title = topic.Title
            })
            .ToListAsync(cancellationToken);
    }

    public void CachePreview(InterviewQuestionImportPreviewDto preview, string userId)
    {
        ArgumentNullException.ThrowIfNull(preview);

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("An authenticated administrator is required.", nameof(userId));
        }

        if (string.IsNullOrWhiteSpace(preview.PreviewToken))
        {
            preview.PreviewToken = Guid.NewGuid().ToString("N");
        }

        _cache.Set(
            GetCacheKey(preview.PreviewToken),
            new CacheEntry(userId, preview),
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = PreviewLifetime
            });
    }

    public InterviewQuestionImportPreviewDto? GetPreview(string previewToken, string userId)
    {
        if (string.IsNullOrWhiteSpace(previewToken) || string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        if (!_cache.TryGetValue<CacheEntry>(GetCacheKey(previewToken), out var entry)
            || entry is null)
        {
            return null;
        }

        if (!string.Equals(entry.UserId, userId, StringComparison.Ordinal)
            || !string.Equals(entry.Preview.PreviewToken, previewToken, StringComparison.Ordinal))
        {
            return null;
        }

        return entry.Preview;
    }

    public async Task<InterviewQuestionImportPreviewDto> PreviewAsync(
        int noteId,
        string fileName,
        Stream fileStream,
        string userId,
        CancellationToken cancellationToken = default,
        Func<string, CancellationToken, Task>? reportProgress = null)
    {
        async Task ReportProgressAsync(string message)
        {
            if (reportProgress is not null)
            {
                await reportProgress(message, cancellationToken);
            }
        }

        var preview = new InterviewQuestionImportPreviewDto
        {
            NoteId = noteId,
            FileName = Path.GetFileName(fileName)
        };

        if (string.IsNullOrWhiteSpace(userId))
        {
            preview.FileErrors.Add("Your administrator session could not be verified. Sign in again.");
            return preview;
        }

        if (noteId <= 0)
        {
            preview.FileErrors.Add("Select a Notes chapter before uploading the workbook.");
            return preview;
        }

        await ReportProgressAsync("Checking the selected Notes chapter...");
        var chapter = await _dbContext.TrainingNotes.AsNoTracking()
            .Where(item => item.Id == noteId && item.Flag == 0 && item.Topic.Flag == 0)
            .Select(item => new
            {
                item.Id,
                item.TopicId,
                TopicTitle = item.Topic.Title,
                item.ChapterId,
                ChapterTitle = item.Title
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (chapter is null)
        {
            preview.FileErrors.Add("The selected Notes chapter is no longer active. Refresh the page and select another chapter.");
            return preview;
        }

        preview.TopicId = chapter.TopicId;
        preview.TopicTitle = chapter.TopicTitle;
        preview.ChapterTitle = $"{chapter.TopicTitle} / {chapter.ChapterId} — {chapter.ChapterTitle}";

        var extension = Path.GetExtension(fileName);
        if (!extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            preview.FileErrors.Add("Choose an Excel .xlsx file using the required Question and Answer columns.");
            return preview;
        }

        await ReportProgressAsync(
            "Opening the Excel workbook and locating the first worksheet...");

        using var workbookBuffer = new MemoryStream();
        await fileStream.CopyToAsync(workbookBuffer, cancellationToken);
        workbookBuffer.Position = 0;

        var parsedExcel = await ExcelQuestionImportMapper.ParseInterviewQuestionsAsync(
            workbookBuffer,
            cancellationToken);

        preview.FileErrors.AddRange(parsedExcel.FileErrors);
        preview.Questions.AddRange(parsedExcel.Questions);

        await ReportProgressAsync(
            "Read " + preview.Questions.Count + " Excel interview row(s). Validating required Question and Answer cells...");

        if (preview.Questions.Count > 0)
        {
            MarkDuplicatesWithinUpload(preview.Questions);

            await ReportProgressAsync("Checking for questions already linked to this Notes chapter...");
            var existingQuestions = await _dbContext.ContentInterviewQuestions
                .AsNoTracking()
                .Where(question => question.NoteId == noteId
                    || (question.Content != null && question.Content.NoteId == noteId))
                .Select(question => question.Question)
                .ToListAsync(cancellationToken);

            var existingKeys = existingQuestions
                .Select(NormalizeQuestion)
                .Where(key => key.Length > 0)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var item in preview.Questions)
            {
                if (existingKeys.Contains(NormalizeQuestion(item.Question)))
                {
                    item.IsDuplicateInDatabase = true;
                    AddIssue(item, "This question already exists for the selected chapter, including archived questions.");
                }
            }
        }

        await ReportProgressAsync("Preparing the preview. No interview questions have been saved yet.");
        return preview;
    }

    public async Task<InterviewQuestionImportOutcomeDto> ConfirmAsync(
        string previewToken,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(previewToken) || string.IsNullOrWhiteSpace(userId))
        {
            return InterviewQuestionImportOutcomeDto.Failure(
                "The import preview is no longer available. Upload the file again.");
        }

        if (!_cache.TryGetValue<CacheEntry>(GetCacheKey(previewToken), out var entry)
            || entry is null)
        {
            return InterviewQuestionImportOutcomeDto.Failure(
                "The import preview has expired or is no longer available. Upload the file again.");
        }

        if (!string.Equals(entry.UserId, userId, StringComparison.Ordinal)
            || !string.Equals(entry.Preview.PreviewToken, previewToken, StringComparison.Ordinal))
        {
            return InterviewQuestionImportOutcomeDto.Failure(
                "This preview is invalid or belongs to another administrator. Upload the file again.");
        }

        var preview = entry.Preview;
        if (!preview.CanImport)
        {
            return InterviewQuestionImportOutcomeDto.Failure(
                "The preview contains errors or duplicate questions. Review the issues before importing.",
                preview);
        }

        var chapterExists = await _dbContext.TrainingNotes.AsNoTracking()
            .AnyAsync(item => item.Id == preview.NoteId && item.Flag == 0 && item.Topic.Flag == 0, cancellationToken);
        if (!chapterExists)
        {
            AddFileError(preview, "The selected Notes chapter is no longer active. Nothing was imported.");
            RefreshCachedPreview(previewToken, entry);
            return InterviewQuestionImportOutcomeDto.Failure(preview.FileErrors[0], preview);
        }

        var existingQuestions = await _dbContext.ContentInterviewQuestions
            .AsNoTracking()
            .Where(question => question.NoteId == preview.NoteId
                || (question.Content != null && question.Content.NoteId == preview.NoteId))
            .Select(question => question.Question)
            .ToListAsync(cancellationToken);

        var currentKeys = existingQuestions
            .Select(NormalizeQuestion)
            .Where(key => key.Length > 0)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var item in preview.Questions)
        {
            if (currentKeys.Contains(NormalizeQuestion(item.Question)))
            {
                item.IsDuplicateInDatabase = true;
                AddIssue(item, "This question now exists for the selected chapter. Nothing was imported; review the duplicate and upload again.");
            }
        }

        if (!preview.CanImport)
        {
            RefreshCachedPreview(previewToken, entry);
            return InterviewQuestionImportOutcomeDto.Failure(
                "One or more questions now duplicate existing interview questions. Nothing was imported.",
                preview);
        }

        var createdAt = DateTime.Now;
        var questionsToInsert = preview.Questions
            .Select(item => new ContentInterviewQuestion
            {
                ContentId = null,
                TopicId = null,
                NoteId = preview.NoteId,
                Question = item.Question.Trim(),
                Answer = item.Answer.Trim(),
                Flag = 0,
                CreatedAt = createdAt
            })
            .ToList();

        _dbContext.ContentInterviewQuestions.AddRange(questionsToInsert);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            foreach (var question in questionsToInsert)
            {
                _dbContext.Entry(question).State = EntityState.Detached;
            }

            const string errorMessage =
                "The database rejected the interview-question import. No partial batch was committed. Review the database error before retrying.";
            AddFileError(preview, errorMessage);
            RefreshCachedPreview(previewToken, entry);
            return InterviewQuestionImportOutcomeDto.Failure(errorMessage, preview);
        }

        _cache.Remove(GetCacheKey(previewToken));
        return InterviewQuestionImportOutcomeDto.Success(questionsToInsert.Count);
    }

    private static void MarkDuplicatesWithinUpload(IReadOnlyList<InterviewQuestionImportItemDto> questions)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in questions)
        {
            var key = NormalizeQuestion(item.Question);
            if (key.Length > 0 && !seen.Add(key))
            {
                AddIssue(item, "This question is repeated in the uploaded batch.");
            }
        }
    }

    private static void AddIssue(InterviewQuestionImportItemDto item, string issue)
    {
        if (!item.Issues.Contains(issue, StringComparer.Ordinal))
        {
            item.Issues.Add(issue);
        }
    }

    private static void AddFileError(InterviewQuestionImportPreviewDto preview, string error)
    {
        if (!preview.FileErrors.Contains(error, StringComparer.Ordinal))
        {
            preview.FileErrors.Add(error);
        }
    }

    private void RefreshCachedPreview(string previewToken, CacheEntry entry)
    {
        _cache.Set(
            GetCacheKey(previewToken),
            entry,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = PreviewLifetime
            });
    }

    private static string NormalizeQuestion(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : WhitespacePattern
                .Replace(value.Normalize(NormalizationForm.FormKC).Trim(), " ")
                .ToUpperInvariant();
    }

    private static string GetCacheKey(string token) => "admin-interview-import:" + token;

    private sealed record CacheEntry(string UserId, InterviewQuestionImportPreviewDto Preview);
}
