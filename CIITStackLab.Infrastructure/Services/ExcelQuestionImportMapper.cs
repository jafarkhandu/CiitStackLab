using System.Text.RegularExpressions;
using CIITStackLab.Application.DTOs;

namespace CIITStackLab.Infrastructure.Services;

/// <summary>
/// Maps fixed Excel columns to MCQ and interview import DTOs without splitting cell values by line.
/// </summary>
internal static class ExcelQuestionImportMapper
{
    private static readonly string[] McqHeaders =
    {
        "QUESTION", "OPTIONA", "OPTIONB", "OPTIONC", "OPTIOND", "CORRECTANSWER"
    };

    private static readonly string[] InterviewHeaders =
    {
        "QUESTION", "ANSWER"
    };

    private static readonly Regex HeaderCleanupPattern = new(
        @"[^A-Z0-9]",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static async Task<ExcelMcqImportParseResult> ParseMcqsAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        var result = new ExcelMcqImportParseResult();
        IReadOnlyList<ExcelQuestionSheetRow> rows;

        try
        {
            rows = await ExcelQuestionSheetParser.ReadRowsAsync(stream, cancellationToken);
        }
        catch (Exception exception) when (
            exception is InvalidDataException
            or IOException
            or System.Xml.XmlException
            or ArgumentException
            or OverflowException)
        {
            result.FileErrors.Add(
                "The Excel workbook could not be read. Upload a valid .xlsx file with a readable first worksheet.");
            return result;
        }

        if (rows.Count == 0)
        {
            result.FileErrors.Add("The Excel worksheet is empty. Add the required header row and question rows.");
            return result;
        }

        if (!HasExpectedHeaders(rows[0], McqHeaders, out var headerError))
        {
            result.FileErrors.Add(
                "MCQ Excel headers must be in columns A–F in this exact order: Question, Option A, Option B, Option C, Option D, Correct Answer. " +
                headerError);
            return result;
        }

        foreach (var row in rows.Skip(1))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var question = ReadCell(row, 0);
            var optionA = ReadCell(row, 1);
            var optionB = ReadCell(row, 2);
            var optionC = ReadCell(row, 3);
            var optionD = ReadCell(row, 4);
            var answer = ReadCell(row, 5);

            var item = new McqImportQuestionDto
            {
                Number = result.Questions.Count + 1,
                SourceLine = row.RowNumber,
                Question = question,
                Option1 = optionA,
                Option2 = optionB,
                Option3 = optionC,
                Option4 = optionD,
                CorrectOptionNumber = ParseCorrectAnswer(answer)
            };

            AddRequiredCellIssue(item.Issues, question, "Question", row.RowNumber);
            AddRequiredCellIssue(item.Issues, optionA, "Option A", row.RowNumber);
            AddRequiredCellIssue(item.Issues, optionB, "Option B", row.RowNumber);
            AddRequiredCellIssue(item.Issues, optionC, "Option C", row.RowNumber);
            AddRequiredCellIssue(item.Issues, optionD, "Option D", row.RowNumber);

            if (string.IsNullOrWhiteSpace(answer))
            {
                item.Issues.Add($"Excel row {row.RowNumber}: Correct Answer is required.");
            }
            else if (item.CorrectOptionNumber is < 1 or > 4)
            {
                item.Issues.Add(
                    $"Excel row {row.RowNumber}: Correct Answer must be A, B, C, D or 1, 2, 3, 4.");
            }

            result.Questions.Add(item);
        }

        if (result.Questions.Count == 0)
        {
            result.FileErrors.Add("No question rows were found below the MCQ header row.");
        }

        if (result.Questions.Count > 500)
        {
            result.Questions.Clear();
            result.FileErrors.Add("A single Excel upload can contain at most 500 MCQs.");
        }

        return result;
    }

    public static async Task<ExcelInterviewImportParseResult> ParseInterviewQuestionsAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        var result = new ExcelInterviewImportParseResult();
        IReadOnlyList<ExcelQuestionSheetRow> rows;

        try
        {
            rows = await ExcelQuestionSheetParser.ReadRowsAsync(stream, cancellationToken);
        }
        catch (Exception exception) when (
            exception is InvalidDataException
            or IOException
            or System.Xml.XmlException
            or ArgumentException
            or OverflowException)
        {
            result.FileErrors.Add(
                "The Excel workbook could not be read. Upload a valid .xlsx file with a readable first worksheet.");
            return result;
        }

        if (rows.Count == 0)
        {
            result.FileErrors.Add("The Excel worksheet is empty. Add the required header row and 20 question-and-answer rows.");
            return result;
        }

        if (!HasExpectedHeaders(rows[0], InterviewHeaders, out var headerError))
        {
            result.FileErrors.Add(
                "Interview Excel headers must be in columns A–B in this exact order: Question, Answer. " +
                headerError);
            return result;
        }

        foreach (var row in rows.Skip(1))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var question = ReadCell(row, 0);
            var answer = ReadCell(row, 1);
            var item = new InterviewQuestionImportItemDto
            {
                Number = result.Questions.Count + 1,
                SourceLine = row.RowNumber,
                Question = question,
                Answer = answer
            };

            AddRequiredCellIssue(item.Issues, question, "Question", row.RowNumber);
            AddRequiredCellIssue(item.Issues, answer, "Answer", row.RowNumber);
            result.Questions.Add(item);
        }

        if (result.Questions.Count != 20)
        {
            result.FileErrors.Add(
                $"Interview Excel must contain exactly 20 question-and-answer rows. Found {result.Questions.Count}.");
        }

        return result;
    }


    public static async Task<ExcelPracticeProgramImportParseResult> ParsePracticeProgramsAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        var result = new ExcelPracticeProgramImportParseResult();
        IReadOnlyList<ExcelQuestionSheetRow> rows;

        try
        {
            rows = await ExcelQuestionSheetParser.ReadRowsAsync(stream, cancellationToken);
        }
        catch (Exception exception) when (
            exception is InvalidDataException
            or IOException
            or System.Xml.XmlException
            or ArgumentException
            or OverflowException)
        {
            result.FileErrors.Add(
                "The Excel workbook could not be read. Upload a valid .xlsx file with a readable first worksheet.");
            return result;
        }

        if (rows.Count == 0)
        {
            result.FileErrors.Add(
                "The Excel worksheet is empty. Add the required header row and programming-question rows.");
            return result;
        }

        var expectedHeaders = new[] { "QUESTIONTITLE", "QUESTIONDESCRIPTION" };
        if (!HasExpectedHeaders(rows[0], expectedHeaders, out var headerError))
        {
            result.FileErrors.Add(
                "Practice-program Excel headers must be in columns A–B in this exact order: Question Title, Question Description. " +
                headerError);
            return result;
        }

        foreach (var row in rows.Skip(1))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var item = new PracticeProgramImportQuestionDto
            {
                Number = result.Questions.Count + 1,
                SourceRow = row.RowNumber,
                QuestionTitle = ReadCell(row, 0),
                QuestionDescription = ReadCell(row, 1)
            };

            AddRequiredCellIssue(item.Issues, item.QuestionTitle, "Question Title", row.RowNumber);
            AddRequiredCellIssue(item.Issues, item.QuestionDescription, "Question Description", row.RowNumber);
            result.Questions.Add(item);
        }

        if (result.Questions.Count == 0)
        {
            result.FileErrors.Add("No programming-question rows were found below the header row.");
        }

        if (result.Questions.Count > 500)
        {
            result.Questions.Clear();
            result.FileErrors.Add("A single Excel upload can contain at most 500 programming questions.");
        }

        return result;
    }

    private static bool HasExpectedHeaders(
        ExcelQuestionSheetRow row,
        IReadOnlyList<string> expectedHeaders,
        out string error)
    {
        for (var index = 0; index < expectedHeaders.Count; index++)
        {
            var actual = NormalizeHeader(ReadCell(row, index));
            if (!string.Equals(actual, expectedHeaders[index], StringComparison.Ordinal))
            {
                error = $"Column {(char)('A' + index)} must be '{FormatHeader(expectedHeaders[index])}'.";
                return false;
            }
        }

        if (row.Cells.Any(cell => cell.Key >= expectedHeaders.Count
                                  && !string.IsNullOrWhiteSpace(cell.Value)))
        {
            error = "Remove extra populated columns from the worksheet.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static string ReadCell(ExcelQuestionSheetRow row, int columnIndex)
    {
        return row.Cells.TryGetValue(columnIndex, out var value)
            ? NormalizeCellValue(value)
            : string.Empty;
    }

    private static string NormalizeCellValue(string value)
    {
        // Keep embedded newlines intact; they belong to this cell, not to another question.
        return value.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();
    }

    private static string NormalizeHeader(string value)
    {
        return HeaderCleanupPattern.Replace(value.Trim().ToUpperInvariant(), string.Empty);
    }

    private static string FormatHeader(string normalizedHeader)
    {
        return normalizedHeader switch
        {
            "QUESTION" => "Question",
            "OPTIONA" => "Option A",
            "OPTIONB" => "Option B",
            "OPTIONC" => "Option C",
            "OPTIOND" => "Option D",
            "CORRECTANSWER" => "Correct Answer",
            "ANSWER" => "Answer",
            _ => normalizedHeader
        };
    }

    private static int ParseCorrectAnswer(string answer)
    {
        var normalized = answer.Trim().TrimEnd('.', ')', ']').ToUpperInvariant();
        if (normalized.Length == 1 && normalized[0] is >= 'A' and <= 'D')
        {
            return normalized[0] - 'A' + 1;
        }

        return int.TryParse(normalized, out var numericAnswer)
               && numericAnswer is >= 1 and <= 4
            ? numericAnswer
            : 0;
    }

    private static void AddRequiredCellIssue(
        ICollection<string> issues,
        string value,
        string columnName,
        int rowNumber)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            issues.Add($"Excel row {rowNumber}: {columnName} is required.");
        }
    }
}

internal sealed class ExcelMcqImportParseResult
{
    public List<McqImportQuestionDto> Questions { get; } = new();

    public List<string> FileErrors { get; } = new();
}

internal sealed class ExcelInterviewImportParseResult
{
    public List<InterviewQuestionImportItemDto> Questions { get; } = new();

    public List<string> FileErrors { get; } = new();
}

internal sealed class ExcelPracticeProgramImportParseResult
{
    public List<PracticeProgramImportQuestionDto> Questions { get; } = new();

    public List<string> FileErrors { get; } = new();
}
