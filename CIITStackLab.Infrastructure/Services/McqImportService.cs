using System.Text;
using System.Text.RegularExpressions;
using CIITStackLab.Application.DTOs;
using CIITStackLab.Application.Interfaces;
using CIITStackLab.Domain.Entities;
using CIITStackLab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CIITStackLab.Infrastructure.Services;

public sealed class McqImportService : IMcqImportService
{
    private static readonly TimeSpan PreviewLifetime = TimeSpan.FromMinutes(20);

    private static readonly Regex WhitespacePattern = new(
        @"\s+",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly ApplicationDbContext _dbContext;
    private readonly IMemoryCache _cache;

    public McqImportService(
        ApplicationDbContext dbContext,
        IMemoryCache cache)
    {
        _dbContext = dbContext;
        _cache = cache;
    }

    public void CachePreview(
        McqImportPreviewDto preview,
        string userId)
    {
        ArgumentNullException.ThrowIfNull(preview);

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException(
                "An authenticated administrator is required.",
                nameof(userId));
        }

        if (string.IsNullOrWhiteSpace(preview.PreviewToken))
        {
            preview.PreviewToken = Guid.NewGuid().ToString("N");
        }

        _cache.Set(
            GetCacheKey(preview.PreviewToken),
            new McqImportCacheEntry(userId, preview),
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = PreviewLifetime
            });
    }

    public McqImportPreviewDto? GetPreview(
        string previewToken,
        string userId)
    {
        if (string.IsNullOrWhiteSpace(previewToken)
            || string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        if (!_cache.TryGetValue<McqImportCacheEntry>(
                GetCacheKey(previewToken),
                out var cacheEntry)
            || cacheEntry is null)
        {
            return null;
        }

        if (!string.Equals(cacheEntry.UserId, userId, StringComparison.Ordinal)
            || !string.Equals(
                cacheEntry.Preview.PreviewToken,
                previewToken,
                StringComparison.Ordinal))
        {
            return null;
        }

        return cacheEntry.Preview;
    }

    public async Task<McqImportPreviewDto> PreviewAsync(
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

        var preview = new McqImportPreviewDto
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
            preview.FileErrors.Add("Select an active Notes chapter before uploading the MCQ workbook.");
            return preview;
        }

        await ReportProgressAsync(
            "Checking that the selected Notes chapter is active...");

        var chapter = await _dbContext.Lessons
            .AsNoTracking()
            .Where(note => note.Id == noteId && note.Flag == 0 && note.TopicId.HasValue && note.Topic.Flag == 0)
            .Select(note => new
            {
                note.Id,
                TopicId = note.TopicId!.Value,
                TopicTitle = note.Topic.Title,
                ChapterId = note.Id.ToString(),
                ChapterTitle = note.Title
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (chapter is null)
        {
            preview.FileErrors.Add("The selected Notes chapter is no longer active. Refresh the page and select another chapter.");
            return preview;
        }

        preview.TopicId = chapter.TopicId;
        preview.TopicLabel = chapter.TopicTitle;
        preview.ChapterLabel = $"{chapter.TopicTitle} / {chapter.ChapterId} — {chapter.ChapterTitle}";

        var extension = Path.GetExtension(fileName);
        if (!extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            preview.FileErrors.Add("Choose an Excel .xlsx file using the required Question, Option A–D and Correct Answer columns.");
            CachePreview(preview, userId);
            return preview;
        }

        await ReportProgressAsync(
            "Opening the Excel workbook and locating the first worksheet...");

        using var workbookBuffer = new MemoryStream();
        await fileStream.CopyToAsync(workbookBuffer, cancellationToken);
        workbookBuffer.Position = 0;

        var parsedExcel = await ExcelQuestionImportMapper.ParseMcqsAsync(
            workbookBuffer,
            cancellationToken);

        preview.FileErrors.AddRange(parsedExcel.FileErrors);
        preview.Questions.AddRange(parsedExcel.Questions);

        await ReportProgressAsync(
            "Read " + preview.Questions.Count + " Excel MCQ row(s). Validating each fixed column, answer key, and repeated question...");

        MarkDuplicatesWithinUpload(preview.Questions);

        await ReportProgressAsync(
            "Checking the selected chapter for duplicate questions...");

        var existingQuestions = await _dbContext.ContentQuestions
            .AsNoTracking()
            .Where(question =>
                question.NoteId == noteId
                || (question.ContentId.HasValue
                    && _dbContext.Lessons.Any(lesson =>
                        lesson.Id == question.ContentId.Value
                        && lesson.Id == noteId)))
            .Select(question => new
            {
                question.Id,
                question.Question,
                question.Flag
            })
            .ToListAsync(cancellationToken);

        var existingQuestionKeys = existingQuestions
            .Select(question => NormalizeQuestion(question.Question))
            .Where(key => key.Length > 0)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var question in preview.Questions)
        {
            var key = NormalizeQuestion(question.Question);

            if (key.Length > 0 && existingQuestionKeys.Contains(key))
            {
                question.IsDuplicateInDatabase = true;
                AddIssue(
                    question,
                    "This question already exists for the selected chapter, including archived MCQs.");
            }
        }

        await ReportProgressAsync(
            "Preparing the review results. No questions have been saved yet.");

        CachePreview(preview, userId);
        return preview;
    }

    public async Task<McqImportOutcomeDto> ConfirmAsync(
        string previewToken,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(previewToken)
            || string.IsNullOrWhiteSpace(userId))
        {
            return McqImportOutcomeDto.Failure(
                "The import preview is no longer available. Upload the file again.");
        }

        if (!_cache.TryGetValue<McqImportCacheEntry>(
                GetCacheKey(previewToken),
                out var cacheEntry)
            || cacheEntry is null)
        {
            return McqImportOutcomeDto.Failure(
                "The import preview has expired or is no longer available. Upload the file again.");
        }

        if (!string.Equals(cacheEntry.UserId, userId, StringComparison.Ordinal))
        {
            return McqImportOutcomeDto.Failure(
                "This preview belongs to another administrator. Upload the file again using your account.");
        }

        var preview = cacheEntry.Preview;

        if (!string.Equals(
                preview.PreviewToken,
                previewToken,
                StringComparison.Ordinal))
        {
            return McqImportOutcomeDto.Failure(
                "The import preview token is invalid. Upload the file again.");
        }

        if (!preview.CanImport)
        {
            return McqImportOutcomeDto.Failure(
                "The file has validation errors or duplicate questions. Fix them and upload the corrected file before importing.",
                preview);
        }

        var chapterExists = await _dbContext.Lessons
            .AsNoTracking()
            .AnyAsync(
                note => note.Id == preview.NoteId && note.Flag == 0 && note.TopicId.HasValue && note.Topic.Flag == 0,
                cancellationToken);

        if (!chapterExists)
        {
            preview.FileErrors.Add(
                "The target Notes chapter is no longer active. Nothing was imported.");
            RefreshCachedPreview(previewToken, cacheEntry);
            return McqImportOutcomeDto.Failure(
                preview.FileErrors[0],
                preview);
        }

        var existingQuestions = await _dbContext.ContentQuestions
            .AsNoTracking()
            .Where(question =>
                question.NoteId == preview.NoteId
                || (question.ContentId.HasValue
                    && _dbContext.Lessons.Any(lesson =>
                        lesson.Id == question.ContentId.Value
                        && lesson.Id == preview.NoteId)))
            .Select(question => question.Question)
            .ToListAsync(cancellationToken);

        var currentQuestionKeys = existingQuestions
            .Select(NormalizeQuestion)
            .Where(key => key.Length > 0)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var question in preview.Questions)
        {
            var key = NormalizeQuestion(question.Question);

            if (key.Length > 0 && currentQuestionKeys.Contains(key))
            {
                question.IsDuplicateInDatabase = true;
                AddIssue(
                    question,
                    "This question now exists for the selected chapter. Nothing was imported; remove or edit the duplicate and upload again.");
            }
        }

        if (!preview.CanImport)
        {
            RefreshCachedPreview(previewToken, cacheEntry);
            return McqImportOutcomeDto.Failure(
                "One or more questions now duplicate existing MCQs. Nothing was imported.",
                preview);
        }

        var createdAt = DateTime.Now;
        var questionsToInsert = preview.Questions
            .Select(question => new ContentQuestion
            {
                NoteId = preview.NoteId,
                TopicId = null,
                ContentId = null,
                Question = question.Question.Trim(),
                Option1 = question.Option1.Trim(),
                Option2 = question.Option2.Trim(),
                Option3 = question.Option3.Trim(),
                Option4 = question.Option4.Trim(),
                CorrectOptionNumber = question.CorrectOptionNumber,
                Flag = 0,
                CreatedAt = createdAt
            })
            .ToList();

        _dbContext.ContentQuestions.AddRange(questionsToInsert);

        try
        {
            // EF Core wraps this single SaveChanges call in a transaction, so the batch
            // is committed together or rolled back together if an insert fails.
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            foreach (var question in questionsToInsert)
            {
                _dbContext.Entry(question).State = EntityState.Detached;
            }

            const string databaseFailureMessage =
                "The database rejected the import. No partial batch was committed. Check the database setup and review the error before retrying.";

            AddFileError(preview, databaseFailureMessage);
            RefreshCachedPreview(previewToken, cacheEntry);

            return McqImportOutcomeDto.Failure(
                databaseFailureMessage,
                preview);
        }

        _cache.Remove(GetCacheKey(previewToken));

        return McqImportOutcomeDto.Success(questionsToInsert.Count);
    }

    private static void AddFileError(
        McqImportPreviewDto preview,
        string error)
    {
        if (!preview.FileErrors.Contains(error, StringComparer.Ordinal))
        {
            preview.FileErrors.Add(error);
        }
    }

    private void RefreshCachedPreview(
        string previewToken,
        McqImportCacheEntry cacheEntry)
    {
        _cache.Set(
            GetCacheKey(previewToken),
            cacheEntry,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = PreviewLifetime
            });
    }

    private static void MarkDuplicatesWithinUpload(
        IReadOnlyList<McqImportQuestionDto> questions)
    {
        var duplicateGroups = questions
            .Select(question => new
            {
                Question = question,
                Key = NormalizeQuestion(question.Question)
            })
            .Where(item => item.Key.Length > 0)
            .GroupBy(item => item.Key, StringComparer.Ordinal)
            .Where(group => group.Count() > 1);

        foreach (var group in duplicateGroups)
        {
            foreach (var item in group)
            {
                AddIssue(
                    item.Question,
                    "This question appears more than once in the uploaded file.");
            }
        }
    }

    private static string GetOption(
        McqImportParsedQuestion question,
        char label)
    {
        return question.Options.TryGetValue(label, out var option)
            ? option.Trim()
            : string.Empty;
    }

    private static string NormalizeQuestion(string? question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return string.Empty;
        }

        var normalized = question
            .Normalize(NormalizationForm.FormKC)
            .Trim();

        return WhitespacePattern
            .Replace(normalized, " ")
            .ToUpperInvariant();
    }

    private static void AddIssue(
        McqImportQuestionDto question,
        string issue)
    {
        if (!question.Issues.Contains(issue, StringComparer.Ordinal))
        {
            question.Issues.Add(issue);
        }
    }

    private static string GetCacheKey(string previewToken)
    {
        return $"admin-mcq-import:{previewToken}";
    }

    private sealed record McqImportCacheEntry(
        string UserId,
        McqImportPreviewDto Preview);
}
