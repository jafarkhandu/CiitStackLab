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
    private readonly IInterviewQuestionAnalyzerService _analyzerService;

    public InterviewQuestionImportService(
        ApplicationDbContext dbContext,
        IMemoryCache cache,
        IInterviewQuestionAnalyzerService analyzerService)
    {
        _dbContext = dbContext;
        _cache = cache;
        _analyzerService = analyzerService;
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
        int topicId,
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
            TopicId = topicId,
            FileName = Path.GetFileName(fileName)
        };

        if (string.IsNullOrWhiteSpace(userId))
        {
            preview.FileErrors.Add("Your administrator session could not be verified. Sign in again.");
            return preview;
        }

        if (topicId <= 0)
        {
            preview.FileErrors.Add("Select the chapter before uploading the notes.");
            return preview;
        }

        await ReportProgressAsync("Checking the selected chapter...");
        var topic = await _dbContext.Topics.AsNoTracking()
            .Where(item => item.Id == topicId && item.Flag == 0)
            .Select(item => new { item.Id, item.Title })
            .SingleOrDefaultAsync(cancellationToken);

        if (topic is null)
        {
            preview.FileErrors.Add("The selected chapter is no longer active. Refresh the page and choose another chapter.");
            return preview;
        }

        preview.TopicTitle = topic.Title;

        var extension = Path.GetExtension(fileName);
        if (!extension.Equals(".txt", StringComparison.OrdinalIgnoreCase)
            && !extension.Equals(".docx", StringComparison.OrdinalIgnoreCase))
        {
            preview.FileErrors.Add("Choose a .txt or .docx file.");
            return preview;
        }

        await ReportProgressAsync("Extracting readable text from the uploaded notes...");

        using var documentBuffer = new MemoryStream();
        await fileStream.CopyToAsync(documentBuffer, cancellationToken);
        documentBuffer.Position = 0;

        string sourceText;
        try
        {
            sourceText = await McqImportParser.ExtractTextAsync(
                documentBuffer,
                extension,
                cancellationToken);
        }
        catch (DecoderFallbackException)
        {
            preview.FileErrors.Add("The text file is not valid UTF-8. Save it as UTF-8 and upload it again.");
            return preview;
        }
        catch (InvalidDataException)
        {
            preview.FileErrors.Add("The Word document is invalid or could not be opened. Upload a valid .docx file.");
            return preview;
        }
        catch (XmlException)
        {
            preview.FileErrors.Add("The Word document contains invalid document XML.");
            return preview;
        }
        catch (IOException)
        {
            preview.FileErrors.Add("The uploaded file could not be read. Please try again.");
            return preview;
        }

        if (string.IsNullOrWhiteSpace(sourceText))
        {
            preview.FileErrors.Add("The document did not contain readable text. Upload non-empty chapter notes.");
            return preview;
        }

        await ReportProgressAsync("Identifying concepts and preparing source-based interview questions...");

        try
        {
            var analyzedQuestions = await _analyzerService.AnalyzeAsync(
                topic.Title,
                sourceText,
                cancellationToken,
                (message, _) => ReportProgressAsync(message));

            preview.Questions.AddRange(analyzedQuestions);
        }
        catch (InvalidOperationException exception)
        {
            preview.FileErrors.Add(exception.Message);
        }

        if (preview.Questions.Count > 0)
        {
            MarkDuplicatesWithinUpload(preview.Questions);

            await ReportProgressAsync("Checking for questions already linked to this chapter...");
            var existingQuestions = await _dbContext.ContentInterviewQuestions
                .AsNoTracking()
                .Where(question => question.TopicId == topicId
                    || (question.Content != null && question.Content.TopicId == topicId))
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

        var topicExists = await _dbContext.Topics.AsNoTracking()
            .AnyAsync(item => item.Id == preview.TopicId && item.Flag == 0, cancellationToken);
        if (!topicExists)
        {
            AddFileError(preview, "The selected chapter is no longer active. Nothing was imported.");
            RefreshCachedPreview(previewToken, entry);
            return InterviewQuestionImportOutcomeDto.Failure(preview.FileErrors[0], preview);
        }

        var existingQuestions = await _dbContext.ContentInterviewQuestions
            .AsNoTracking()
            .Where(question => question.TopicId == preview.TopicId
                || (question.Content != null && question.Content.TopicId == preview.TopicId))
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
                TopicId = preview.TopicId,
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
