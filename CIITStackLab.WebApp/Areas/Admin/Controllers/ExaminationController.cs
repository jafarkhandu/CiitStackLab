using System.Globalization;
using System.Security.Claims;
using System.Data.Common;
using System.Text;
using System.IO.Compression;
using System.Xml.Linq;
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
                QuestionCount = _db.TopicExamQuestions.Count(q => q.Flag == 0 && q.TopicId == x.Id)
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
            TempData["ExamAdminError"] = "Select a topic and a non-empty Excel file.";
            return RedirectToAction(nameof(Index));
        }

        var topic = await _db.Topics.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == topicId && x.Flag == 0, cancellationToken);
        if (topic is null)
        {
            TempData["ExamAdminError"] = "The selected topic is not active.";
            return RedirectToAction(nameof(Index));
        }
        if (!Path.GetExtension(file.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            TempData["ExamAdminError"] = "Upload an .xlsx Excel workbook. The first worksheet must contain: Question, Option1, Option2, Option3, Option4, CorrectOptionNumber.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var rows = ReadExcelRows(file.OpenReadStream());
            if (rows.Count == 0)
            {
                TempData["ExamAdminError"] = "The first worksheet is empty.";
                return RedirectToAction(nameof(Index));
            }
            var expected = new[] { "Question", "Option1", "Option2", "Option3", "Option4", "CorrectOptionNumber" };
            var actualHeaders = rows[0].Select(x => x.Trim().TrimStart('\uFEFF')).ToList();
            var headerIssues = new List<string>();
            foreach (var requiredHeader in expected)
            {
                if (!actualHeaders.Contains(requiredHeader, StringComparer.OrdinalIgnoreCase))
                {
                    headerIssues.Add($"Missing required column '{requiredHeader}'. Rename the matching column to '{requiredHeader}'.");
                }
            }
            foreach (var actualHeader in actualHeaders.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                if (!expected.Contains(actualHeader, StringComparer.OrdinalIgnoreCase))
                {
                    headerIssues.Add($"Unexpected column '{actualHeader}'. Use only these required headers: {string.Join(", ", expected)}.");
                }
            }
            if (actualHeaders.Count != expected.Length)
            {
                headerIssues.Add($"The first row must contain exactly {expected.Length} columns in this order: {string.Join(", ", expected)}.");
            }
            else if (!actualHeaders.SequenceEqual(expected, StringComparer.OrdinalIgnoreCase))
            {
                headerIssues.Add($"The column names are present but in the wrong order. Required order: {string.Join(", ", expected)}. Current order: {string.Join(", ", actualHeaders)}.");
            }

            var existingKeys = await _db.TopicExamQuestions.AsNoTracking()
                .Where(q => q.Flag == 0 && q.TopicId == topicId)
                .Select(q => q.Question).ToListAsync(cancellationToken);
            var keys = existingKeys.Select(Normalize).Where(x => x.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var batchKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var additions = new List<TopicExamQuestion>();
            var analysisRows = new List<ExamImportRow>();
            var rowNumber = 1;
            var invalidRows = 0;

            if (headerIssues.Count == 0)
            {
                foreach (var fields in rows.Skip(1))
                {
                    rowNumber++;
                    if (fields.All(string.IsNullOrWhiteSpace)) continue;

                    var question = GetField(fields, 0);
                    var option1 = GetField(fields, 1);
                    var option2 = GetField(fields, 2);
                    var option3 = GetField(fields, 3);
                    var option4 = GetField(fields, 4);
                    var answerText = GetField(fields, 5);
                    var issues = new List<string>();
                    var correct = 0;
                    var normalizedAnswer = answerText.Trim();
                    if (normalizedAnswer.Length == 1 && normalizedAnswer[0] is >= 'A' and <= 'D')
                    {
                        correct = normalizedAnswer[0] - 'A' + 1;
                    }
                    else if (normalizedAnswer.Length == 1 && normalizedAnswer[0] is >= 'a' and <= 'd')
                    {
                        correct = normalizedAnswer[0] - 'a' + 1;
                    }
                    else if (!int.TryParse(normalizedAnswer, NumberStyles.Integer, CultureInfo.InvariantCulture, out correct))
                    {
                        correct = 0;
                    }

                    if (fields.Count != 6)
                        issues.Add($"This row has {fields.Count} columns; it must have exactly 6. Check for an extra comma or missing cell.");
                    if (string.IsNullOrWhiteSpace(question))
                        issues.Add("Question is missing. Enter the full question in the Question column.");
                    if (string.IsNullOrWhiteSpace(option1)) issues.Add("Option1 (A) is missing. Add the first answer choice.");
                    if (string.IsNullOrWhiteSpace(option2)) issues.Add("Option2 (B) is missing. Add the second answer choice.");
                    if (string.IsNullOrWhiteSpace(option3)) issues.Add("Option3 (C) is missing. Add the third answer choice.");
                    if (string.IsNullOrWhiteSpace(option4)) issues.Add("Option4 (D) is missing. Add the fourth answer choice.");
                    var options = new[] { option1, option2, option3, option4 };
                    if (options.All(x => !string.IsNullOrWhiteSpace(x))
                        && options.Select(Normalize).Distinct(StringComparer.OrdinalIgnoreCase).Count() != 4)
                        issues.Add("Answer choices contain duplicates. Provide four distinct options in Option1–Option4.");
                    if (correct is < 1 or > 4)
                        issues.Add($"CorrectOptionNumber is '{answerText}', but it must be 1, 2, 3, or 4 (1=A, 2=B, 3=C, 4=D). You may also enter A, B, C, or D.");

                    var key = Normalize(question);
                    var isDuplicate = key.Length > 0 && (keys.Contains(key) || !batchKeys.Add(key));
                    if (isDuplicate)
                        issues.Add("This question duplicates a question already stored for the selected topic or another row in this workbook. Rewrite it or remove the duplicate row.");

                    var valid = issues.Count == 0;
                    analysisRows.Add(new ExamImportRow(rowNumber, question, option1, option2, option3, option4,
                        correct is >= 1 and <= 4 ? correct : null, valid, isDuplicate, issues));
                    if (!valid)
                    {
                        invalidRows++;
                        continue;
                    }

                    additions.Add(new TopicExamQuestion
                    {
                        TopicId = topicId, Question = question.Trim(),
                        Option1 = option1.Trim(), Option2 = option2.Trim(),
                        Option3 = option3.Trim(), Option4 = option4.Trim(),
                        CorrectOptionNumber = correct, Flag = 0, CreatedAtUtc = DateTime.UtcNow
                    });
                }
            }

            var previewToken = Guid.NewGuid().ToString("N");
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized();

            var preview = new ExamImportPreview(userId, topicId, topic.Title, additions, invalidRows,
                analysisRows, headerIssues, Path.GetFileName(file.FileName));
            _cache.Set(GetPreviewKey(previewToken), preview, TimeSpan.FromMinutes(20));
            TempData["ExamAdminMessage"] = $"Analysis complete: {additions.Count} valid questions; {invalidRows} rows need attention. Nothing has been saved yet.";
            return RedirectToAction(nameof(Index), new { previewToken });
        }
        catch (Exception exception) when (exception is InvalidDataException or System.Xml.XmlException)
        {
            TempData["ExamAdminError"] = "The uploaded file is not a valid .xlsx workbook or its first worksheet could not be read.";
        }
        catch (Exception exception) when (exception is DbUpdateException or DbException or IOException)
        {
            _logger.LogError(exception, "Exam question import failed for topic {TopicId}.", topicId);
            TempData["ExamAdminError"] = "Import failed. No success was reported; check the database and uploaded Excel workbook.";
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

        var existingQuestions = await _db.TopicExamQuestions.AsNoTracking()
            .Where(q => q.Flag == 0 && q.TopicId == preview.TopicId)
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
            _db.TopicExamQuestions.AddRange(preview.Questions);
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

    private static string GetField(IReadOnlyList<string> fields, int index) =>
        index < fields.Count ? fields[index].Trim() : string.Empty;

    private static string Normalize(string? value) =>
        string.Join(' ', (value ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Trim();

    private static List<List<string>> ReadExcelRows(Stream stream)
    {
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        XNamespace main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XNamespace rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        XNamespace packageRel = "http://schemas.openxmlformats.org/package/2006/relationships";

        var workbookEntry = archive.GetEntry("xl/workbook.xml") ?? throw new InvalidDataException();
        var relationshipsEntry = archive.GetEntry("xl/_rels/workbook.xml.rels") ?? throw new InvalidDataException();
        XDocument workbook;
        XDocument relationships;
        using (var reader = workbookEntry.Open()) workbook = XDocument.Load(reader);
        using (var reader = relationshipsEntry.Open()) relationships = XDocument.Load(reader);

        var firstSheet = workbook.Root?.Element(main + "sheets")?.Elements(main + "sheet").FirstOrDefault();
        var relationshipId = (string?)firstSheet?.Attribute(rel + "id");
        if (string.IsNullOrWhiteSpace(relationshipId)) throw new InvalidDataException();

        var target = relationships.Root?.Elements(packageRel + "Relationship")
            .Where(x => (string?)x.Attribute("Id") == relationshipId)
            .Select(x => (string?)x.Attribute("Target")).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(target)) throw new InvalidDataException();

        var worksheetPath = target.StartsWith("/", StringComparison.Ordinal)
            ? target.TrimStart('/')
            : "xl/" + target;
        var pathParts = new List<string>();
        foreach (var part in worksheetPath.Split('/'))
        {
            if (part == "..")
            {
                if (pathParts.Count > 0) pathParts.RemoveAt(pathParts.Count - 1);
            }
            else if (part != "." && part.Length > 0)
            {
                pathParts.Add(part);
            }
        }
        worksheetPath = string.Join("/", pathParts);
        var sheetEntry = archive.GetEntry(worksheetPath) ?? throw new InvalidDataException();

        var sharedStrings = new List<string>();
        if (archive.GetEntry("xl/sharedStrings.xml") is { } sharedEntry)
        {
            using var reader = sharedEntry.Open();
            var sharedDoc = XDocument.Load(reader);
            sharedStrings = sharedDoc.Root?.Elements(main + "si")
                .Select(si => string.Concat(si.Descendants(main + "t").Select(t => t.Value)))
                .ToList() ?? new List<string>();
        }

        using var sheetReader = sheetEntry.Open();
        var sheet = XDocument.Load(sheetReader);
        var rows = new List<List<string>>();
        foreach (var row in sheet.Descendants(main + "sheetData").Elements(main + "row"))
        {
            var cells = new SortedDictionary<int, string>();
            foreach (var cell in row.Elements(main + "c"))
            {
                var reference = (string?)cell.Attribute("r") ?? string.Empty;
                var column = 0;
                foreach (var ch in reference.TakeWhile(char.IsLetter))
                    column = column * 26 + char.ToUpperInvariant(ch) - 'A' + 1;
                if (column <= 0) continue;

                var type = (string?)cell.Attribute("t");
                var value = cell.Element(main + "v")?.Value ?? string.Empty;
                if (type == "s" && int.TryParse(value, out var sharedIndex)
                    && sharedIndex >= 0 && sharedIndex < sharedStrings.Count)
                {
                    value = sharedStrings[sharedIndex];
                }
                else if (type == "inlineStr")
                {
                    value = string.Concat(cell.Descendants(main + "t").Select(t => t.Value));
                }
                cells[column - 1] = value;
            }

            var maxColumn = cells.Count == 0 ? 0 : cells.Keys.Max() + 1;
            rows.Add(Enumerable.Range(0, maxColumn)
                .Select(index => cells.GetValueOrDefault(index, string.Empty)).ToList());
        }
        return rows;
    }

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

    public sealed record ExamImportRow(
        int RowNumber,
        string Question,
        string Option1,
        string Option2,
        string Option3,
        string Option4,
        int? CorrectOptionNumber,
        bool IsValid,
        bool IsDuplicate,
        List<string> Issues);

    public sealed record ExamImportPreview(
        string UserId,
        int TopicId,
        string TopicTitle,
        List<TopicExamQuestion> Questions,
        int InvalidRows,
        List<ExamImportRow> Rows,
        List<string> HeaderIssues,
        string FileName);

    public sealed class ExaminationTopicRow
    {
        public int Id { get; init; }
        public string Title { get; init; } = string.Empty;
        public int QuestionCount { get; init; }
    }
}
