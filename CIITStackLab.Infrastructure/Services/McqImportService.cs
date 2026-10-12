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

    public async Task<McqImportPreviewDto> PreviewAsync(
        int contentId,
        string fileName,
        Stream fileStream,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var preview = new McqImportPreviewDto
        {
            ContentId = contentId,
            FileName = Path.GetFileName(fileName)
        };

        if (string.IsNullOrWhiteSpace(userId))
        {
            preview.FileErrors.Add("Your administrator session could not be verified. Sign in again.");
            return preview;
        }

        if (contentId <= 0)
        {
            preview.FileErrors.Add("Select an active content item before uploading the MCQ file.");
            return preview;
        }

        var content = await _dbContext.Lessons
            .AsNoTracking()
            .Where(lesson =>
                lesson.Id == contentId
                && lesson.Flag == 0
                && lesson.Topic != null
                && lesson.Topic.Flag == 0)
            .Select(lesson => new
            {
                lesson.Id,
                ContentTitle = lesson.Title ?? string.Empty,
                TopicTitle = lesson.Topic!.Title
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (content is null)
        {
            preview.FileErrors.Add("The selected content is no longer active. Refresh the page and select another item.");
            return preview;
        }

        preview.ContentLabel = $"{content.TopicTitle} / {content.ContentTitle}";

        var extension = Path.GetExtension(fileName);

        if (!extension.Equals(".txt", StringComparison.OrdinalIgnoreCase)
            && !extension.Equals(".docx", StringComparison.OrdinalIgnoreCase))
        {
            preview.FileErrors.Add("Choose a .txt or .docx file.");
            return preview;
        }

        var parsed = await McqImportParser.ParseAsync(
            fileStream,
            extension,
            cancellationToken);

        preview.FileErrors.AddRange(parsed.FileErrors);

        foreach (var parsedQuestion in parsed.Questions)
        {
            var question = new McqImportQuestionDto
            {
                Number = parsedQuestion.Number,
                SourceLine = parsedQuestion.SourceLine,
                Question = parsedQuestion.Question.Trim(),
                Option1 = GetOption(parsedQuestion, 'A'),
                Option2 = GetOption(parsedQuestion, 'B'),
                Option3 = GetOption(parsedQuestion, 'C'),
                Option4 = GetOption(parsedQuestion, 'D'),
                CorrectOptionNumber = parsedQuestion.CorrectOptionNumber ?? 0
            };

            question.Issues.AddRange(parsedQuestion.Issues);
            preview.Questions.Add(question);
        }

        MarkDuplicatesWithinUpload(preview.Questions);

        var existingQuestions = await _dbContext.ContentQuestions
            .AsNoTracking()
            .Where(question => question.ContentId == contentId)
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
                    "This question already exists for the selected content, including archived MCQs.");
            }
        }

        if (preview.Questions.Count > 0)
        {
            preview.PreviewToken = Guid.NewGuid().ToString("N");

            _cache.Set(
                GetCacheKey(preview.PreviewToken),
                new McqImportCacheEntry(userId, preview),
                new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = PreviewLifetime
                });
        }

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

        var contentExists = await _dbContext.Lessons
            .AsNoTracking()
            .AnyAsync(
                lesson =>
                    lesson.Id == preview.ContentId
                    && lesson.Flag == 0
                    && lesson.Topic != null
                    && lesson.Topic.Flag == 0,
                cancellationToken);

        if (!contentExists)
        {
            preview.FileErrors.Add(
                "The target content is no longer active. Nothing was imported.");
            RefreshCachedPreview(previewToken, cacheEntry);
            return McqImportOutcomeDto.Failure(
                preview.FileErrors[0],
                preview);
        }

        var existingQuestions = await _dbContext.ContentQuestions
            .AsNoTracking()
            .Where(question => question.ContentId == preview.ContentId)
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
                    "This question now exists in the database. Nothing was imported; remove or edit the duplicate and upload again.");
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
                ContentId = preview.ContentId,
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

            return McqImportOutcomeDto.Failure(
                "The database rejected the import. No partial batch was committed. Review the content and try again.",
                preview);
        }

        _cache.Remove(GetCacheKey(previewToken));

        return McqImportOutcomeDto.Success(questionsToInsert.Count);
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
