using System.Globalization;
using System.Text;
using CIITStackLab.Domain.Entities;
using CIITStackLab.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CIITStackLab.WebApp.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,Super User")]
[Route("Admin/Examination")]
public sealed class ExaminationController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<ExaminationController> _logger;

    public ExaminationController(ApplicationDbContext db, ILogger<ExaminationController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
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

            _db.ContentQuestions.AddRange(additions);
            await _db.SaveChangesAsync(cancellationToken);
            TempData["ExamAdminMessage"] = $"Imported {additions.Count} questions for {topic.Title}. Invalid or duplicate rows skipped: {invalidRows}.";
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

    public sealed class ExaminationTopicRow
    {
        public int Id { get; init; }
        public string Title { get; init; } = string.Empty;
        public int QuestionCount { get; init; }
    }
}
