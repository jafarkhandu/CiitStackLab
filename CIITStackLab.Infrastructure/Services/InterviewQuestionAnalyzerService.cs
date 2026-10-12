using System.Text;
using System.Text.RegularExpressions;
using CIITStackLab.Application.DTOs;
using CIITStackLab.Application.Interfaces;

namespace CIITStackLab.Infrastructure.Services;

/// <summary>
/// Produces reviewable interview Q&A from statements and terms present in the uploaded text.
/// This deterministic service does not call a remote AI provider.
/// </summary>
public sealed class InterviewQuestionAnalyzerService : IInterviewQuestionAnalyzerService
{
    private const int RequiredQuestionCount = 20;

    private static readonly Regex SentenceBoundaryPattern = new(
        @"(?<=[.!?])\s+(?=[A-Z0-9""“'(])",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex RelationPattern = new(
        @"^(?:(?:the|a|an|this|these|those|each|every)\s+)?(?<subject>[A-Za-z][A-Za-z0-9+#./_-]*(?:\s+[A-Za-z0-9+#./_-]+){0,3})\s+(?<relation>is defined as|is known as|is called|refers to|means|denotes|represents|is|are|was|were|includes|consists of|contains|provides|uses|supports|allows|enables|converts|stores|requires|connects|controls|manages|performs|creates|produces|helps|prevents|measures|describes|defines)\s+(?<predicate>[^.!?;]{8,260})[.!?]?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex QuotedTermPattern = new(
        @"""(?<term>[^""\r\n]{2,60})""|“(?<term>[^”\r\n]{2,60})”",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex AcronymPattern = new(
        @"\b[A-Z][A-Z0-9]{1,7}\b",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex MultiWordTermPattern = new(
        @"\b[A-Z][A-Za-z0-9+#.-]*(?:\s+[A-Z][A-Za-z0-9+#.-]*){1,3}\b",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex WordPattern = new(
        @"\b[A-Za-z][A-Za-z0-9+#./_-]{3,}\b",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex WhitespacePattern = new(
        @"\s+",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex LeadingNoisePattern = new(
        @"^(?:the|a|an|this|these|those|each|every|in|on|at|for|from|to|with|by|of|as|when|while|if|because|according to)\s+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "A", "AN", "THE", "THIS", "THAT", "THESE", "THOSE", "EACH", "EVERY",
        "IT", "THEY", "WE", "YOU", "I", "HE", "SHE", "WHICH", "WHAT", "WHEN",
        "WHERE", "WHO", "HOW", "THERE", "HERE", "CHAPTER", "TOPIC", "INTRODUCTION",
        "CONCLUSION", "EXAMPLE", "EXAMPLES", "NOTE", "OVERVIEW", "IN", "ON", "AT",
        "FOR", "FROM", "TO", "WITH", "BY", "OF", "AS", "WHILE", "IF", "BECAUSE",
        "ABOUT", "ABOVE", "AFTER", "AGAIN", "AGAINST", "ALL", "ALMOST", "ALONG",
        "ALREADY", "ALSO", "ALTHOUGH", "ALWAYS", "AMONG", "AND", "ANY", "ARE",
        "AROUND", "BE", "BECAME", "BECOME", "BEEN", "BEFORE", "BEING", "BELOW",
        "BETWEEN", "BOTH", "BUT", "CAN", "CANNOT", "COULD", "DID", "DO", "DOES",
        "DOING", "DOWN", "DURING", "EITHER", "ELSE", "ENOUGH", "EVEN", "EVER",
        "FEW", "FURTHER", "GET", "GOT", "HAD", "HAS", "HAVE", "HAVING", "HENCE",
        "HER", "HERS", "HIM", "HIS", "HOWEVER", "INTO", "ITS", "ITSELF", "JUST",
        "LESS", "MANY", "MAY", "MIGHT", "MORE", "MOST", "MUCH", "MUST", "NEAR",
        "NEVER", "NOT", "NOW", "OFF", "ONCE", "ONLY", "ONTO", "OR", "OTHER",
        "OTHERS", "OUR", "OURS", "OUT", "OVER", "OWN", "PER", "RATHER", "REALLY",
        "SAME", "SEVERAL", "SHALL", "SHOULD", "SINCE", "SO", "SOME", "SOMETHING",
        "SOMETIMES", "STILL", "SUCH", "TAKE", "THEN", "THEREFORE", "THROUGH",
        "THROUGHOUT", "THUS", "TOO", "TOWARD", "TOWARDS", "UNDER", "UNTIL", "UP",
        "UPON", "US", "USE", "USED", "USES", "USING", "VERY", "VIA", "WANT",
        "WANTS", "WAS", "WERE", "WILL", "WITHIN", "WITHOUT", "WOULD", "YES",
        "YET", "YOUR", "YOURS", "INCLUDING", "INCLUDE", "INCLUDES", "FOLLOWING",
        "FIRST", "SECOND", "THIRD", "NEXT", "VARIOUS", "ONLY", "BEING", "LIKE"
    };

    public async Task<IReadOnlyList<InterviewQuestionImportItemDto>> AnalyzeAsync(
        string contentTitle,
        string sourceText,
        CancellationToken cancellationToken = default,
        Func<string, CancellationToken, Task>? reportProgress = null)
    {
        if (string.IsNullOrWhiteSpace(sourceText) || sourceText.Trim().Length < 100)
        {
            throw new InvalidOperationException(
                "The file does not contain enough readable material. Upload fuller .txt or .docx content. No interview questions were saved.");
        }

        await ReportAsync(
            reportProgress,
            "Normalizing content and identifying complete factual statements...",
            cancellationToken);

        var statements = ExtractStatements(sourceText);
        if (statements.Count < 5)
        {
            throw new InvalidOperationException(
                "The local analyzer found too few complete statements to create reliable interview Q&A. Upload fuller notes with explanatory sentences. No questions were saved.");
        }

        var facts = ExtractFacts(statements);
        var terms = ExtractTerms(contentTitle, sourceText, facts);
        if (terms.Count < 4)
        {
            throw new InvalidOperationException(
                "The local analyzer found too few distinct concepts in this file. Upload notes with more named concepts, definitions, or factual statements. No questions were saved.");
        }

        await ReportAsync(
            reportProgress,
            "Found " + statements.Count + " readable statements and " + terms.Count +
            " candidate concepts. Building question-and-answer pairs from the source...",
            cancellationToken);

        var questions = new List<InterviewQuestionImportItemDto>();
        var questionKeys = new HashSet<string>(StringComparer.Ordinal);
        var factsByLine = facts
            .GroupBy(fact => fact.LineNumber)
            .ToDictionary(group => group.Key, group => group.ToList());

        // First pass: one primary question per source statement to preserve topic coverage.
        foreach (var statement in statements)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (questions.Count >= RequiredQuestionCount)
            {
                break;
            }

            string? questionText = null;

            if (factsByLine.TryGetValue(statement.LineNumber, out var lineFacts))
            {
                var matchingFact = lineFacts.FirstOrDefault(fact =>
                    string.Equals(
                        NormalizeKey(fact.Statement),
                        NormalizeKey(statement.Text),
                        StringComparison.Ordinal));

                if (matchingFact is not null)
                {
                    questionText = "What is " + matchingFact.Subject + "?";
                }
            }

            if (questionText is null)
            {
                var primaryTerm = terms
                    .Where(term => ContainsWholeTerm(statement.Text, term))
                    .OrderByDescending(term => term.Length)
                    .FirstOrDefault();

                if (primaryTerm is not null)
                {
                    questionText = "What does the chapter explain about " + primaryTerm + "?";
                }
            }

            if (questionText is not null)
            {
                TryAddQuestion(
                    questions,
                    questionKeys,
                    questionText,
                    statement.Text,
                    statement.LineNumber);
            }
        }

        // Second pass: use additional distinct terms from each statement only if more items are needed.
        foreach (var statement in statements)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (questions.Count >= RequiredQuestionCount)
            {
                break;
            }

            var matchingTerms = terms
                .Where(term => ContainsWholeTerm(statement.Text, term))
                .OrderByDescending(term => term.Length)
                .ToList();

            foreach (var term in matchingTerms)
            {
                if (questions.Count >= RequiredQuestionCount)
                {
                    break;
                }

                TryAddQuestion(
                    questions,
                    questionKeys,
                    "How is " + term + " described in the chapter?",
                    statement.Text,
                    statement.LineNumber);
            }

            if (questions.Count > 0 && questions.Count % 5 == 0)
            {
                await ReportAsync(
                    reportProgress,
                    "Built " + questions.Count + " distinct interview questions so far...",
                    cancellationToken);
            }
        }

        // Final safe fallback: ask about an explicitly extracted definition/fact; answers remain source text.
        foreach (var fact in facts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (questions.Count >= RequiredQuestionCount)
            {
                break;
            }

            TryAddQuestion(
                questions,
                questionKeys,
                "How does the chapter describe " + fact.Subject + "?",
                fact.Statement,
                fact.LineNumber);
        }

        if (questions.Count != RequiredQuestionCount)
        {
            throw new InvalidOperationException(
                "The local analyzer could build only " + questions.Count +
                " distinct source-based interview questions. It will not invent the remaining items. Upload fuller notes with more complete statements and distinct concepts. No questions were saved.");
        }

        await ReportAsync(
            reportProgress,
            "Created 20 question-and-answer pairs. Validating empty fields and repeated questions...",
            cancellationToken);

        ValidateQuestions(questions);

        await ReportAsync(
            reportProgress,
            "Local analysis is complete. Review all 20 Q&A pairs; nothing has been saved yet.",
            cancellationToken);

        return questions;
    }

    private static List<SourceStatement> ExtractStatements(string sourceText)
    {
        var statements = new List<SourceStatement>();
        var lines = sourceText
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');

        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var line = lines[lineIndex].Trim();
            if (line.Length == 0
                || Regex.IsMatch(line, @"^(?:CHAPTER|TOPIC)\s*[:#-]", RegexOptions.IgnoreCase))
            {
                continue;
            }

            line = Regex.Replace(line, @"^(?:[-*•]\s*|\d+[.)]\s*)", string.Empty);
            foreach (var part in SentenceBoundaryPattern.Split(line))
            {
                var text = WhitespacePattern.Replace(part, " ")
                    .Trim()
                    .Trim('"', '“', '”', ' ');

                if (text.Length < 35 || text.Length > 380
                    || text.EndsWith("?", StringComparison.Ordinal)
                    || Regex.IsMatch(text, @"^(?:Q(?:UESTION)?\s*\d*\s*[:.)-])", RegexOptions.IgnoreCase))
                {
                    continue;
                }

                var wordCount = Regex.Matches(text, @"\b[\p{L}\p{N}][\p{L}\p{N}+#./_-]*\b").Count;
                if (wordCount < 6)
                {
                    continue;
                }

                if (!statements.Any(statement =>
                    string.Equals(statement.Text, text, StringComparison.OrdinalIgnoreCase)))
                {
                    statements.Add(new SourceStatement(text, lineIndex + 1));
                }
            }
        }

        return statements;
    }

    private static List<SourceFact> ExtractFacts(IReadOnlyList<SourceStatement> statements)
    {
        var facts = new List<SourceFact>();
        var keys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var statement in statements)
        {
            var match = RelationPattern.Match(statement.Text);
            if (!match.Success)
            {
                continue;
            }

            var subject = CleanTerm(match.Groups["subject"].Value);
            var predicate = match.Groups["predicate"].Value.Trim().TrimEnd('.', '!', '?', ';', ':');
            if (!IsUsefulTerm(subject) || predicate.Length < 8)
            {
                continue;
            }

            var key = NormalizeKey(subject + "|" + predicate);
            if (keys.Add(key))
            {
                facts.Add(new SourceFact(subject, predicate, statement.Text, statement.LineNumber));
            }
        }

        return facts;
    }

    private static List<string> ExtractTerms(
        string contentTitle,
        string sourceText,
        IReadOnlyList<SourceFact> facts)
    {
        var terms = new List<string>();
        var known = new HashSet<string>(StringComparer.Ordinal);

        AddTerm(contentTitle, terms, known);
        foreach (var fact in facts)
        {
            AddTerm(fact.Subject, terms, known);
        }

        foreach (Match match in QuotedTermPattern.Matches(sourceText))
        {
            var termGroup = match.Groups["term"];
            if (termGroup.Success)
            {
                AddTerm(termGroup.Value, terms, known);
            }
        }

        foreach (Match match in AcronymPattern.Matches(sourceText))
        {
            AddTerm(match.Value, terms, known);
        }

        foreach (Match match in MultiWordTermPattern.Matches(sourceText))
        {
            AddTerm(match.Value, terms, known);
        }

        var frequentTerms = WordPattern.Matches(sourceText)
            .Select(match => CleanTerm(match.Value))
            .Where(IsUsefulTerm)
            .GroupBy(NormalizeKey, StringComparer.Ordinal)
            .Select(group => new { Term = group.First(), Count = group.Count() })
            .Where(item => item.Count >= 2)
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Term, StringComparer.OrdinalIgnoreCase)
            .Take(60);

        foreach (var item in frequentTerms)
        {
            AddTerm(item.Term, terms, known);
        }

        return terms;
    }

    private static void AddTerm(
        string? candidate,
        ICollection<string> terms,
        ISet<string> known)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return;
        }

        var term = CleanTerm(candidate);
        if (!IsUsefulTerm(term))
        {
            return;
        }

        if (known.Add(NormalizeKey(term)))
        {
            terms.Add(term);
        }
    }

    private static string CleanTerm(string value)
    {
        var term = value.Trim().Trim('"', '“', '”', '\'', '‘', '’', ',', ';', ':', ' ');
        term = LeadingNoisePattern.Replace(term, string.Empty);
        return WhitespacePattern.Replace(term, " ").Trim().TrimEnd(',', ';', ':', '.');
    }

    private static bool IsUsefulTerm(string term)
    {
        if (term.Length < 2 || term.Length > 60 || StopWords.Contains(NormalizeKey(term)))
        {
            return false;
        }

        var words = term.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length <= 5
            && words.Length > 0
            && !StopWords.Contains(NormalizeKey(words[0]));
    }

    private static void TryAddQuestion(
        ICollection<InterviewQuestionImportItemDto> questions,
        ISet<string> questionKeys,
        string questionText,
        string answerText,
        int sourceLine)
    {
        var question = WhitespacePattern.Replace(questionText.Trim(), " ");
        var answer = WhitespacePattern.Replace(answerText.Trim(), " ");

        if (question.Length < 8 || answer.Length < 15 || answer.Length > 380
            || !questionKeys.Add(NormalizeKey(question)))
        {
            return;
        }

        questions.Add(new InterviewQuestionImportItemDto
        {
            Number = questions.Count + 1,
            SourceLine = Math.Max(0, sourceLine),
            Question = question,
            Answer = answer
        });
    }

    private static void ValidateQuestions(IReadOnlyList<InterviewQuestionImportItemDto> questions)
    {
        if (questions.Count != RequiredQuestionCount)
        {
            throw new InvalidOperationException(
                "The local analyzer did not create exactly 20 interview Q&A pairs. Nothing was saved.");
        }

        var questionKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in questions)
        {
            if (string.IsNullOrWhiteSpace(item.Question)
                || string.IsNullOrWhiteSpace(item.Answer)
                || !questionKeys.Add(NormalizeKey(item.Question)))
            {
                throw new InvalidOperationException(
                    "The local analyzer produced an empty or repeated question/answer. Nothing was saved; upload clearer notes and try again.");
            }
        }
    }

    private static bool ContainsWholeTerm(string text, string term)
    {
        var pattern = @"(?<![\p{L}\p{N}_])" + Regex.Escape(term) + @"(?![\p{L}\p{N}_])";
        return Regex.IsMatch(
            text,
            pattern,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static string NormalizeKey(string value)
    {
        return WhitespacePattern
            .Replace(value.Normalize(NormalizationForm.FormKC).Trim(), " ")
            .ToUpperInvariant();
    }

    private static Task ReportAsync(
        Func<string, CancellationToken, Task>? reportProgress,
        string message,
        CancellationToken cancellationToken)
    {
        return reportProgress is null
            ? Task.CompletedTask
            : reportProgress(message, cancellationToken);
    }

    private sealed record SourceStatement(string Text, int LineNumber);

    private sealed record SourceFact(
        string Subject,
        string Predicate,
        string Statement,
        int LineNumber);
}
