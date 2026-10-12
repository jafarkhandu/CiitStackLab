using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace CIITStackLab.Infrastructure.Services;

internal static class McqImportParser
{
    private const int MaximumQuestionCount = 500;
    private const long MaximumDocumentXmlLength = 10 * 1024 * 1024;

    private static readonly XNamespace WordNamespace =
        "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    private static readonly Regex QuestionHeaderPattern = new(
        @"^(?:Q(?:UESTION)?\s*\d*\s*[:.)-]\s*|\d+\s*[.)]\s*)(.*)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex OptionPattern = new(
        @"^([A-D])\s*[).:\-]\s*(.*)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex AnswerPattern = new(
        @"^(?:CORRECT\s+)?ANSWER\s*[:=\-]\s*(.+)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static async Task<McqImportParseResult> ParseAsync(
        Stream stream,
        string extension,
        CancellationToken cancellationToken = default)
    {
        var result = new McqImportParseResult();
        IReadOnlyList<string> lines;

        try
        {
            if (extension.Equals(".txt", StringComparison.OrdinalIgnoreCase))
            {
                using var reader = new StreamReader(
                    stream,
                    new UTF8Encoding(
                        encoderShouldEmitUTF8Identifier: false,
                        throwOnInvalidBytes: true),
                    detectEncodingFromByteOrderMarks: true,
                    bufferSize: 1024,
                    leaveOpen: true);

                var text = await reader.ReadToEndAsync(cancellationToken);
                lines = SplitTextLines(text);
            }
            else if (extension.Equals(".docx", StringComparison.OrdinalIgnoreCase))
            {
                lines = ReadWordParagraphs(stream);
            }
            else
            {
                result.FileErrors.Add("Only .txt and .docx files are supported.");
                return result;
            }
        }
        catch (DecoderFallbackException)
        {
            result.FileErrors.Add(
                "The text file is not valid UTF-8. Save it as UTF-8 and upload it again.");
            return result;
        }
        catch (InvalidDataException)
        {
            result.FileErrors.Add(
                "The Word document is invalid or could not be opened. Upload a valid .docx file.");
            return result;
        }
        catch (XmlException)
        {
            result.FileErrors.Add(
                "The Word document contains invalid document XML.");
            return result;
        }
        catch (IOException)
        {
            result.FileErrors.Add(
                "The uploaded file could not be read. Please try again.");
            return result;
        }
        catch (UnauthorizedAccessException)
        {
            result.FileErrors.Add(
                "The uploaded file could not be read. Please try again.");
            return result;
        }

        ParseLines(lines, result);

        if (result.Questions.Count == 0 && result.FileErrors.Count == 0)
        {
            result.FileErrors.Add(
                "No MCQs were detected. Check that the file follows the required question, A–D options and ANSWER format.");
        }

        if (result.Questions.Count > MaximumQuestionCount)
        {
            result.Questions.Clear();
            result.FileErrors.Add(
                $"A single upload can contain at most {MaximumQuestionCount} questions. Split the file into smaller batches.");
        }

        return result;
    }

    private static IReadOnlyList<string> SplitTextLines(string text)
    {
        return text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n')
            .Select(line => line.Replace('\u00A0', ' ').Trim())
            .ToList();
    }

    private static IReadOnlyList<string> ReadWordParagraphs(Stream stream)
    {
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        var documentEntry = archive.GetEntry("word/document.xml");

        if (documentEntry is null)
        {
            throw new InvalidDataException("The required Word document XML is missing.");
        }

        if (documentEntry.Length > MaximumDocumentXmlLength)
        {
            throw new InvalidDataException("The Word document XML is too large.");
        }

        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = MaximumDocumentXmlLength
        };

        using var entryStream = documentEntry.Open();
        using var xmlReader = XmlReader.Create(entryStream, settings);
        var document = XDocument.Load(xmlReader, LoadOptions.None);
        var body = document.Root?.Element(WordNamespace + "body");

        if (body is null)
        {
            throw new InvalidDataException("The Word document body is missing.");
        }

        var lines = new List<string>();

        foreach (var paragraph in body.Descendants(WordNamespace + "p"))
        {
            var paragraphText = new StringBuilder();

            foreach (var node in paragraph.Descendants())
            {
                if (node.Name == WordNamespace + "t")
                {
                    paragraphText.Append(node.Value);
                }
                else if (node.Name == WordNamespace + "tab")
                {
                    paragraphText.Append(' ');
                }
                else if (node.Name == WordNamespace + "br"
                         || node.Name == WordNamespace + "cr")
                {
                    paragraphText.Append('\n');
                }
            }

            foreach (var line in paragraphText.ToString().Split('\n'))
            {
                lines.Add(line.Replace('\u00A0', ' ').Trim());
            }
        }

        return lines;
    }

    private static void ParseLines(
        IReadOnlyList<string> lines,
        McqImportParseResult result)
    {
        McqImportParsedQuestion? current = null;

        void FinishCurrent()
        {
            if (current is null)
            {
                return;
            }

            result.Questions.Add(current);
            current = null;
        }

        for (var lineIndex = 0; lineIndex < lines.Count; lineIndex++)
        {
            var line = lines[lineIndex].Trim();
            var lineNumber = lineIndex + 1;

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (Regex.IsMatch(
                    line,
                    @"^(?:CHAPTER|TOPIC)\s*[:#\-].*$",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                continue;
            }

            var questionMatch = QuestionHeaderPattern.Match(line);

            if (questionMatch.Success)
            {
                FinishCurrent();

                current = new McqImportParsedQuestion
                {
                    Number = result.Questions.Count + 1,
                    Question = questionMatch.Groups[1].Value.Trim(),
                    SourceLine = lineNumber
                };

                if (current.Question.Length == 0)
                {
                    current.Issues.Add(
                        $"Line {lineNumber}: the question text is empty.");
                }

                continue;
            }

            var optionMatch = OptionPattern.Match(line);

            if (optionMatch.Success)
            {
                if (current is null)
                {
                    result.FileErrors.Add(
                        $"Line {lineNumber}: option {optionMatch.Groups[1].Value.ToUpperInvariant()} appears before a question header.");
                    continue;
                }

                var label = optionMatch.Groups[1].Value.ToUpperInvariant()[0];
                var optionText = optionMatch.Groups[2].Value.Trim();

                if (current.HasAnswerLine)
                {
                    current.Issues.Add(
                        $"Line {lineNumber}: options must appear before the ANSWER line.");
                    continue;
                }

                if (current.Options.ContainsKey(label))
                {
                    current.Issues.Add(
                        $"Line {lineNumber}: option {label} is defined more than once.");
                    continue;
                }

                current.Options[label] = optionText;

                if (optionText.Length == 0)
                {
                    current.Issues.Add(
                        $"Line {lineNumber}: option {label} is empty.");
                }

                continue;
            }

            var answerMatch = AnswerPattern.Match(line);

            if (answerMatch.Success)
            {
                if (current is null)
                {
                    result.FileErrors.Add(
                        $"Line {lineNumber}: answer key appears before a question header.");
                    continue;
                }

                if (current.HasAnswerLine)
                {
                    current.Issues.Add(
                        $"Line {lineNumber}: more than one answer key was supplied for this question.");
                    continue;
                }

                current.HasAnswerLine = true;

                if (current.Options.Count != 4)
                {
                    current.Issues.Add(
                        $"Line {lineNumber}: the ANSWER line must appear after options A, B, C and D.");
                }

                var answerValue = answerMatch.Groups[1].Value.Trim();
                current.CorrectOptionNumber = ParseAnswerNumber(answerValue);

                if (!current.CorrectOptionNumber.HasValue)
                {
                    current.Issues.Add(
                        $"Line {lineNumber}: answer '{answerValue}' must be A, B, C, D or 1, 2, 3, 4.");
                }

                continue;
            }

            if (current is null)
            {
                result.FileErrors.Add(
                    $"Line {lineNumber}: unexpected text outside an MCQ. Start each question with 'Q1:'.");
                continue;
            }

            var looksLikeMalformedOptionOrAnswer = Regex.IsMatch(
                line,
                @"^(?:[A-Z]\s*[).:\-]|OPTION\s*[A-Z0-9]+\s*[:).:\-]|(?:CORRECT\s+)?ANSWER\b)",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            if (looksLikeMalformedOptionOrAnswer)
            {
                current.Issues.Add(
                    $"Line {lineNumber}: '{LimitText(line, 70)}' has an unrecognized option or answer label. Use A), B), C), D) and ANSWER:.");
            }
            else if (current.Options.Count == 0 && !current.HasAnswerLine)
            {
                current.Question = string.Join(
                    ' ',
                    new[] { current.Question, line }
                        .Where(value => !string.IsNullOrWhiteSpace(value)));
            }
            else
            {
                current.Issues.Add(
                    $"Line {lineNumber}: '{LimitText(line, 70)}' is not a valid A–D option or ANSWER line.");
            }
        }

        FinishCurrent();

        foreach (var question in result.Questions)
        {
            foreach (var label in new[] { 'A', 'B', 'C', 'D' })
            {
                if (!question.Options.ContainsKey(label))
                {
                    question.Issues.Add(
                        $"Option {label} is missing. Every MCQ must have options A, B, C and D.");
                }
            }

            if (!question.HasAnswerLine)
            {
                question.Issues.Add("The ANSWER line is missing.");
            }
            else if (question.CorrectOptionNumber.HasValue)
            {
                var correctLabel = (char)('A' + question.CorrectOptionNumber.Value - 1);
                if (!question.Options.ContainsKey(correctLabel)
                    || string.IsNullOrWhiteSpace(question.Options[correctLabel]))
                {
                    question.Issues.Add(
                        $"The correct answer {correctLabel} does not point to a non-empty option.");
                }
            }

            if (string.IsNullOrWhiteSpace(question.Question))
            {
                question.Issues.Add("Question text is required.");
            }
        }
    }

    private static int? ParseAnswerNumber(string value)
    {
        var normalized = value.Trim().TrimEnd('.', ')', ']').ToUpperInvariant();

        if (normalized.Length == 1 && normalized[0] is >= 'A' and <= 'D')
        {
            return normalized[0] - 'A' + 1;
        }

        if (int.TryParse(normalized, out var numericAnswer)
            && numericAnswer is >= 1 and <= 4)
        {
            return numericAnswer;
        }

        var optionMatch = Regex.Match(
            normalized,
            @"^(?:OPTION\s*)?([A-D])$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        return optionMatch.Success
            ? char.ToUpperInvariant(optionMatch.Groups[1].Value[0]) - 'A' + 1
            : null;
    }

    private static string LimitText(string value, int maximumLength)
    {
        return value.Length <= maximumLength
            ? value
            : value[..maximumLength] + "…";
    }
}

internal sealed class McqImportParseResult
{
    public List<McqImportParsedQuestion> Questions { get; } = new();

    public List<string> FileErrors { get; } = new();
}

internal sealed class McqImportParsedQuestion
{
    public int Number { get; init; }

    public int SourceLine { get; init; }

    public string Question { get; set; } = string.Empty;

    public Dictionary<char, string> Options { get; } = new();

    public int? CorrectOptionNumber { get; set; }

    public bool HasAnswerLine { get; set; }

    public List<string> Issues { get; } = new();
}
