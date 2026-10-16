using System.Globalization;
using System.Security.Claims;
using System.Data.Common;
using System.Text;
using CIITStackLab.Domain.Entities;
using CIITStackLab.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CIITStackLab.WebApp.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,Super User")]
[Route("Admin/Examination")]
public sealed class ExaminationController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<ExaminationController> _logger;
    private readonly IMemoryCache _cache;

    public ExaminationController(ApplicationDbContext db, ILogger<ExaminationController> logger, IMemoryCache cache)
    {
        _db = db;
        _logger = logger;
        _cache = cache;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string? previewToken, CancellationToken cancellationToken)
    {
        ViewBag.Topics = await _db.Topics.AsNoTracking()
            .Where(x => x.Flag == 0).OrderBy(x => x.Title)
            .Select(x => new ExaminationTopicRow
            {
                Id = x.Id, Title = x.Title,
                QuestionCount = _db.ContentQuestions.Count(q => q.Flag == 0
                    && (q.TopicId == x.Id || (q.NoteId.HasValue && _db.TrainingNotes.Any(n => n.Id == q.NoteId && n.TopicId == x.Id))
                    || (q.ContentId.HasValue && _db.Lessons.Any(l => l.Id == q.ContentId && l.TopicId == x.Id))))
            }).ToListAsync(cancellationToken);
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrWhiteSpace(previewToken)
            && _cache.TryGetValue<ExamImportPreview>(GetPreviewKey(previewToken), out var preview)
            && preview is not null
            && string.Equals(preview.UserId, userId, StringComparison.Ordinal))
        {
            ViewBag.Preview = preview;
            ViewBag.PreviewToken = previewToken;
        }
        ViewBag.Message = TempData["ExamAdminMessage"];
        ViewBag.Error = TempData["ExamAdminError"];
        return View();
    }

    [HttpPost("Import")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(50 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 50 * 1024 * 1024)]
    public async Task<IActionResult> Import(int topicId, IFormFile? file, CancellationToken cancellationToken)
    {
        if (topicId <= 0 || file is null || file.Length == 0)
        {
            TempData["ExamAdminError"] = "Select a topic and a non-empty CSV file.";
            return RedirectToAction(nameof(Index));
        }

        var topic = await _db.Topics.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == topicId && x.Flag == 0, cancellationToken);
        if (topic is null)
        {
            TempData["ExamAdminError"] = "The selected topic is not active.";
            return RedirectToAction(nameof(Index));
        }
        if (!Path.GetExtension(file.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase))
        {
            TempData["ExamAdminError"] = "Upload a UTF-8 CSV file. Required columns: Question, Option1, Option2, Option3, Option4, CorrectOptionNumber.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            using var reader = new StreamReader(file.OpenReadStream(), new UTF8Encoding(false, true));
            var header = ParseCsvLine(await reader.ReadLineAsync(cancellationToken) ?? string.Empty);
            var expected = new[] { "Question", "Option1", "Option2", "Option3", "Option4", "CorrectOptionNumber" };
            if (!header.Select(x => x.Trim()).SequenceEqual(expected, StringComparer.OrdinalIgnoreCase))
            {
                TempData["ExamAdminError"] = "CSV headers do not match the required format.";
                return RedirectToAction(nameof(Index));
            }

            var existingKeys = await _db.ContentQuestions.AsNoTracking()
                .Where(q => q.Flag == 0 && (q.TopicId == topicId
                    || (q.NoteId.HasValue && _db.TrainingNotes.Any(n => n.Id == q.NoteId && n.TopicId == topicId))
                    || (q.ContentId.HasValue && _db.Lessons.Any(l => l.Id == q.ContentId && l.TopicId == topicId))))
                .Select(q => q.Question).ToListAsync(cancellationToken);
            var keys = existingKeys.Select(Normalize).Where(x => x.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var batchKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var additions = new List<ContentQuestion>();
            var rowNumber = 1;
            var invalidRows = 0;

            while (await reader.ReadLineAsync(cancellationToken) is { } line)
            {
                rowNumber++;
                if (string.IsNullOrWhiteSpace(line)) continue;
                var fields = ParseCsvLine(line);
                if (fields.Count != 6 || string.IsNullOrWhiteSpace(fields[0])
                    || fields.Skip(1).Take(4).Any(string.IsNullOrWhiteSpace)
                    || !int.TryParse(fields[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out var correct)
                    || correct is < 1 or > 4)
                {
                    invalidRows++;
                    continue;
                }
                var key = Normalize(fields[0]);
                if (key.Length == 0 || keys.Contains(key) || !batchKeys.Add(key))
                {
                    invalidRows++;
                    continue;
                }
                additions.Add(new ContentQuestion
                {
                    TopicId = topicId, Question = fields[0].Trim(),
                    Option1 = fields[1].Trim(), Option2 = fields[2].Trim(),
                    Option3 = fields[3].Trim(), Option4 = fields[4].Trim(),
                    CorrectOptionNumber = correct, Flag = 0, CreatedAt = DateTime.UtcNow
                });
            }

            if (additions.Count == 0)
            {
                TempData["ExamAdminError"] = $"No valid new questions were found. Invalid or duplicate rows: {invalidRows}.";
                return RedirectToAction(nameof(Index));
            }

            var previewToken = Guid.NewGuid().ToString("N");
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized();

            _cache.Set(GetPreviewKey(previewToken),
                new ExamImportPreview(userId, topicId, topic.Title, additions, invalidRows),
                TimeSpan.FromMinutes(20));
            TempData["ExamAdminMessage"] = $"Analysis complete: {additions.Count} valid new questions; {invalidRows} invalid or duplicate rows skipped. Review before confirming.";
            return RedirectToAction(nameof(Index), new { previewToken });
        }
        catch (DecoderFallbackException)
        {
            TempData["ExamAdminError"] = "The CSV is not valid UTF-8. Save it as UTF-8 and upload again.";
        }
        catch (Exception exception) when (exception is DbUpdateException or DbException or IOException)
        {
            _logger.LogError(exception, "Exam question import failed for topic {TopicId}.", topicId);
            TempData["ExamAdminError"] = "Import failed. No success was reported; check the database and uploaded CSV.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("ConfirmImport")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmImport(string previewToken, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(previewToken)
            || !_cache.TryGetValue<ExamImportPreview>(GetPreviewKey(previewToken), out var preview)
            || preview is null
            || !string.Equals(preview.UserId, userId, StringComparison.Ordinal))
        {
            TempData["ExamAdminError"] = "The import preview expired or belongs to another administrator. Analyze the file again.";
            return RedirectToAction(nameof(Index));
        }

        var topicExists = await _db.Topics.AsNoTracking()
            .AnyAsync(t => t.Id == preview.TopicId && t.Flag == 0, cancellationToken);
        if (!topicExists)
        {
            TempData["ExamAdminError"] = "The selected topic is no longer active. Nothing was imported.";
            return RedirectToAction(nameof(Index));
        }

        var existingQuestions = await _db.ContentQuestions.AsNoTracking()
            .Where(q => q.Flag == 0 && (q.TopicId == preview.TopicId
                || (q.NoteId.HasValue && _db.TrainingNotes.Any(n => n.Id == q.NoteId && n.TopicId == preview.TopicId))
                || (q.ContentId.HasValue && _db.Lessons.Any(l => l.Id == q.ContentId && l.TopicId == preview.TopicId))))
            .Select(q => q.Question).ToListAsync(cancellationToken);
        var existingKeys = existingQuestions.Select(Normalize).Where(x => x.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (preview.Questions.Any(q => existingKeys.Contains(Normalize(q.Question))))
        {
            _cache.Remove(GetPreviewKey(previewToken));
            TempData["ExamAdminError"] = "Questions changed in the database after analysis. Nothing was imported; analyze the file again.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            _db.ContentQuestions.AddRange(preview.Questions);
            await _db.SaveChangesAsync(cancellationToken);
            _cache.Remove(GetPreviewKey(previewToken));
            TempData["ExamAdminMessage"] = $"Imported {preview.Questions.Count} questions for {preview.TopicTitle}. Invalid or duplicate rows skipped: {preview.InvalidRows}.";
        }
        catch (Exception exception) when (exception is DbUpdateException or DbException)
        {
            _logger.LogError(exception, "Exam question import confirmation failed for topic {TopicId}.", preview.TopicId);
            TempData["ExamAdminError"] = "The import could not be saved. Check the database and try analyzing the file again.";
        }
        return RedirectToAction(nameof(Index));
    }

    private static string GetPreviewKey(string token) => $"topic-exam-import:{token}";

    private static string Normalize(string? value) =>
        string.Join(' ', (value ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Trim();

    private static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var value = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (quoted && i + 1 < line.Length && line[i + 1] == '"')
                {
                    value.Append('"');
                    i++;
                }
                else quoted = !quoted;
            }
            else if (ch == ',' && !quoted)
            {
                fields.Add(value.ToString());
                value.Clear();
            }
            else value.Append(ch);
        }
        fields.Add(value.ToString());
        return fields;
    }

    public sealed record ExamImportPreview(
        string UserId, int TopicId, string TopicTitle, List<ContentQuestion> Questions, int InvalidRows);

    public sealed class ExaminationTopicRow
    {
        public int Id { get; init; }
        public string Title { get; init; } = string.Empty;
        public int QuestionCount { get; init; }
    }
}
