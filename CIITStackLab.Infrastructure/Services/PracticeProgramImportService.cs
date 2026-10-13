using System.Text.RegularExpressions;
using CIITStackLab.Application.DTOs;
using CIITStackLab.Application.Interfaces;
using CIITStackLab.Domain.Entities;
using CIITStackLab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CIITStackLab.Infrastructure.Services;

public sealed class PracticeProgramImportService : IPracticeProgramImportService
{
    private static readonly TimeSpan PreviewLifetime = TimeSpan.FromMinutes(20);

    private static readonly Regex WhitespacePattern = new(
        @"\s+",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly ApplicationDbContext _dbContext;
    private readonly IMemoryCache _cache;

    public PracticeProgramImportService(
        ApplicationDbContext dbContext,
        IMemoryCache cache)
    {
        _dbContext = dbContext;
        _cache = cache;
    }

    public void CachePreview(PracticeProgramImportPreviewDto preview, string userId)
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
            new CacheEntry(userId, preview),
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = PreviewLifetime
            });
    }

    public PracticeProgramImportPreviewDto? GetPreview(string previewToken, string userId)
    {
        if (string.IsNullOrWhiteSpace(previewToken)
            || string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        if (!_cache.TryGetValue<CacheEntry>(GetCacheKey(previewToken), out var entry)
            || entry is null)
        {
            return null;
        }

        if (!string.Equals(entry.UserId, userId, StringComparison.Ordinal)
            || !string.Equals(
                entry.Preview.PreviewToken,
                previewToken,
                StringComparison.Ordinal))
        {
            return null;
        }

        return entry.Preview;
    }

    public async Task<PracticeProgramImportPreviewDto> PreviewAsync(
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

        var preview = new PracticeProgramImportPreviewDto
        {
            NoteId = noteId,
            FileName = Path.GetFileName(fileName)
        };

        if (noteId <= 0)
        {
            preview.FileErrors.Add("Select a Notes chapter before uploading the workbook.");
            return preview;
        }

        if (!string.Equals(Path.GetExtension(fileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            preview.FileErrors.Add("Choose an Excel .xlsx workbook.");
            return preview;
        }

        await ReportProgressAsync("Checking the selected Notes chapter...");
        var chapter = await _dbContext.TrainingNotes
            .AsNoTracking()
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
            preview.FileErrors.Add(
                "The selected Notes chapter is no longer active. Refresh the page and choose another chapter.");
            return preview;
        }

        preview.TopicId = chapter.TopicId;
        preview.TopicTitle = chapter.TopicTitle;
        preview.ChapterTitle = $"{chapter.TopicTitle} / {chapter.ChapterId} — {chapter.ChapterTitle}";
        preview.ContentTitle = chapter.ChapterTitle;

        await ReportProgressAsync("Opening the Excel workbook and reading the first worksheet...");
        using var workbookBuffer = new MemoryStream();
        await fileStream.CopyToAsync(workbookBuffer, cancellationToken);
        workbookBuffer.Position = 0;

        var parsed = await ExcelQuestionImportMapper.ParsePracticeProgramsAsync(
            workbookBuffer,
            cancellationToken);
        preview.FileErrors.AddRange(parsed.FileErrors);
        preview.Programs.AddRange(parsed.Questions);

        await ReportProgressAsync(
            $"Read {preview.Programs.Count} programming-question row(s). Checking required cells and repeated entries...");

        var existing = await _dbContext.ContentProgramQuestions
            .AsNoTracking()
            .Where(item => item.NoteId == noteId
                || (item.ContentId.HasValue
                    && _dbContext.Lessons.Any(contentItem =>
                        contentItem.Id == item.ContentId.Value
                        && contentItem.NoteId == noteId)))
            .Select(item => new
            {
                item.QuestionTitle,
                item.QuestionDescription
            })
            .ToListAsync(cancellationToken);

        var existingKeys = existing
            .Select(item => NormalizeProgramKey(item.QuestionTitle, item.QuestionDescription))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var uploadKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in preview.Programs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var key = NormalizeProgramKey(item.QuestionTitle, item.QuestionDescription);
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            if (existingKeys.Contains(key))
            {
                item.IsDuplicateInDatabase = true;
                item.Issues.Add(
                    "This programming question already exists for the selected content, including archived questions.");
            }

            if (!uploadKeys.Add(key))
            {
                item.IsDuplicateInUpload = true;
                item.Issues.Add("This programming question is repeated in the uploaded workbook.");
            }
        }

        await ReportProgressAsync("Practice-question validation is complete. No solutions or answers have been created.");
        return preview;
    }

    public async Task<PracticeProgramImportOutcomeDto> ConfirmAsync(
        string previewToken,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var preview = GetPreview(previewToken, userId);
        if (preview is null)
        {
            return new PracticeProgramImportOutcomeDto
            {
                Message = "The import preview expired or is unavailable. Upload the Excel workbook again."
            };
        }

        if (!preview.CanImport)
        {
            return new PracticeProgramImportOutcomeDto
            {
                Message = "Import is blocked. Resolve the validation errors and duplicate questions before confirming.",
                Preview = preview
            };
        }

        var chapterExists = await _dbContext.TrainingNotes
            .AsNoTracking()
            .AnyAsync(item => item.Id == preview.NoteId
                && item.Flag == 0
                && item.Topic.Flag == 0, cancellationToken);

        if (!chapterExists)
        {
            preview.FileErrors.Add(
                "The selected Notes chapter is no longer active. Nothing was imported.");
            return new PracticeProgramImportOutcomeDto
            {
                Message = preview.FileErrors[^1],
                Preview = preview
            };
        }

        var currentPrograms = await _dbContext.ContentProgramQuestions
            .AsNoTracking()
            .Where(item => item.NoteId == preview.NoteId
                || (item.ContentId.HasValue
                    && _dbContext.Lessons.Any(contentItem =>
                        contentItem.Id == item.ContentId.Value
                        && contentItem.NoteId == preview.NoteId)))
            .Select(item => new
            {
                item.QuestionTitle,
                item.QuestionDescription
            })
            .ToListAsync(cancellationToken);

        var currentKeys = currentPrograms
            .Select(item => NormalizeProgramKey(item.QuestionTitle, item.QuestionDescription))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var item in preview.Programs)
        {
            var key = NormalizeProgramKey(item.QuestionTitle, item.QuestionDescription);
            if (currentKeys.Contains(key))
            {
                item.IsDuplicateInDatabase = true;
                if (!item.Issues.Any(issue => issue.Contains("already exists", StringComparison.OrdinalIgnoreCase)))
                {
                    item.Issues.Add(
                        "This programming question now exists for the selected content. Nothing was imported; review the duplicate and upload again.");
                }
            }
        }

        if (!preview.CanImport)
        {
            return new PracticeProgramImportOutcomeDto
            {
                Message = "The question list changed while you were reviewing it. Review the duplicate or validation warnings and try again.",
                Preview = preview
            };
        }

        var programs = preview.Programs.Select(item => new ContentProgramQuestion
        {
            NoteId = preview.NoteId,
            ContentId = null,
            QuestionTitle = item.QuestionTitle.Trim(),
            QuestionDescription = item.QuestionDescription.Trim(),
            Flag = 0,
            CreatedAt = DateTime.Now
        }).ToList();

        // Only question rows are inserted. Solutions remain separate records added through Add Solution.
        _dbContext.ContentProgramQuestions.AddRange(programs);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _cache.Remove(GetCacheKey(previewToken));

        return new PracticeProgramImportOutcomeDto
        {
            Succeeded = true,
            Message = $"{programs.Count} programming question(s) imported successfully. No solutions were added; each question now has its own database ID."
        };
    }

    private static string NormalizeProgramKey(string? title, string? description)
    {
        var normalizedTitle = WhitespacePattern.Replace(title?.Trim() ?? string.Empty, " ");
        var normalizedDescription = WhitespacePattern.Replace(description?.Trim() ?? string.Empty, " ");
        return $"{normalizedTitle}\n{normalizedDescription}".Trim();
    }

    private static string GetCacheKey(string previewToken) =>
        $"admin:practice-program-import:{previewToken}";

    private sealed record CacheEntry(
        string UserId,
        PracticeProgramImportPreviewDto Preview);
}
