using System.Text;
using System.Text.RegularExpressions;
using CIITStackLab.Application.DTOs;
using CIITStackLab.Application.Interfaces;

namespace CIITStackLab.Infrastructure.Services;

/// <summary>
/// Builds reviewable MCQs from facts and terminology explicitly found in chapter text.
/// This service is deterministic and does not call a remote AI provider.
/// </summary>
public sealed class ChapterMcqAnalyzerService : IChapterMcqAnalyzerService
{
    private const int MaximumQuestionCount = 100;

    private static readonly Regex SentenceBoundaryPattern = new(
        @"(?<=[.!?])\s+(?=[A-Z0-9""“'(])",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SubjectRelationPattern = new(
        @"^(?:(?:the|a|an|this|these|those|each|every)\s+)?(?<subject>[A-Za-z][A-Za-z0-9+#./_-]*(?:\s+[A-Za-z0-9+#./_-]+){0,3})\s+(?<relation>is defined as|is known as|is called|refers to|means|denotes|represents|is|are|was|were|includes|consists of|contains|provides|uses|supports|allows|enables|converts|stores|requires|connects|controls|manages|performs|creates|produces|helps|prevents|measures|describes|defines)\s+(?<predicate>[^.!?;]{8,220})[.!?]?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex QuotedTermPattern = new(
        @"[""“](?<term>[^""“”\r\n]{2,60})[""”]",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex AcronymPattern = new(
        @"\b[A-Z][A-Z0-9]{1,7}\b",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex MultiWordTermPattern = new(
        @"\b[A-Z][A-Za-z0-9+#.-]*(?:\s+[A-Z][A-Za-z0-9+#.-]*){1,3}\b",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex CapitalizedWordPattern = new(
        @"\b[A-Z][A-Za-z0-9+#.-]{2,30}\b",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex WhitespacePattern = new(
        @"\s+",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex LeadingNoisePattern = new(
        @"^(?:the|a|an|this|these|those|each|every|in|on|at|for|from|to|with|by|of|as|when|while|if|because|according to)\s+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly HashSet<string> StopTerms = new(StringComparer.Ordinal)
    {
        "A", "AN", "THE", "THIS", "THAT", "THESE", "THOSE", "EACH", "EVERY",
        "IT", "THEY", "WE", "YOU", "I", "HE", "SHE", "WHICH", "WHAT", "WHEN",
        "WHERE", "WHO", "HOW", "THERE", "HERE", "CHAPTER", "TOPIC", "INTRODUCTION",
        "CONCLUSION", "EXAMPLE", "NOTE", "OVERVIEW", "IN", "ON", "AT", "FOR",
        "FROM", "TO", "WITH", "BY", "OF", "AS", "WHILE", "IF", "BECAUSE",
        "ABOUT", "ABOVE", "AFTER", "AGAIN", "AGAINST", "ALL", "ALMOST", "ALONG",
        "ALREADY", "ALSO", "ALTHOUGH", "ALWAYS", "AMONG", "AND", "ANY", "ARE",
        "AROUND", "BE", "BECAME", "BECOME", "BEEN", "BEFORE", "BEING", "BELOW",
        "BETWEEN", "BOTH", "BUT", "CAN", "CANNOT", "COULD", "DID", "DO", "DOES",
        "DOING", "DOWN", "DURING", "EITHER", "ELSE", "ENOUGH", "EVEN", "EVER",
        "FEW", "FURTHER", "GET", "GOT", "HAD", "HAS", "HAVE", "HAVING", "HENCE",
        "HER", "HERE", "HERS", "HIM", "HIS", "HOWEVER", "INTO", "ITS", "ITSELF",
        "JUST", "LESS", "MANY", "MAY", "MIGHT", "MORE", "MOST", "MUCH", "MUST",
        "NEAR", "NEVER", "NOT", "NOW", "OFF", "ONCE", "ONLY", "ONTO", "OR",
        "OTHER", "OTHERS", "OUR", "OURS", "OUT", "OVER", "OWN", "PER", "RATHER",
        "REALLY", "SAME", "SEVERAL", "SHALL", "SHOULD", "SINCE", "SO", "SOME",
        "SOMETHING", "SOMETIMES", "STILL", "SUCH", "TAKE", "THEN", "THEREFORE",
        "THROUGH", "THROUGHOUT", "THUS", "TOO", "TOWARD", "TOWARDS", "UNDER",
        "UNTIL", "UP", "UPON", "US", "USE", "USED", "USES", "USING", "VERY",
        "VIA", "WANT", "WANTS", "WAS", "WERE", "WILL", "WITHIN", "WITHOUT",
        "WOULD", "YES", "YET", "YOUR", "YOURS", "INCLUDING", "INCLUDE", "INCLUDES",
        "EXAMPLES", "FOLLOWING", "FIRST", "SECOND", "THIRD", "NEXT", "VARIOUS"
    };

    public async Task<IReadOnlyList<McqImportQuestionDto>> AnalyzeAsync(
        string chapterTitle,
        string sourceText,
        CancellationToken cancellationToken = default,
        Func<string, CancellationToken, Task>? reportProgress = null)
    {
        if (string.IsNullOrWhiteSpace(sourceText) || sourceText.Trim().Length < 100)
        {
            throw new InvalidOperationException(
                "The document does not contain enough readable chapter text. Upload fuller .txt or .docx chapter notes. No MCQs were saved.");
        }

        await ReportAsync(
            reportProgress,
            "Normalizing chapter text and identifying complete source statements...",
            cancellationToken);

        var statements = ExtractStatements(sourceText);
        if (statements.Count < 4)
        {
            throw new InvalidOperationException(
                "The local analyzer found too few complete statements to build reliable MCQs. Upload fuller chapter notes with factual sentences or definitions. No MCQs were saved.");
        }

        var facts = ExtractFacts(statements);
        var terms = ExtractTerms(chapterTitle, sourceText, facts);

        if (terms.Count < 4)
        {
            throw new InvalidOperationException(
                "The local analyzer found fewer than four distinct chapter concepts to use as answer choices. Upload fuller notes containing more named concepts, terms, or definitions. No MCQs were saved.");
        }

        await ReportAsync(
            reportProgress,
            "Found " + statements.Count + " source statements and " + terms.Count +
            " candidate terms. Building questions directly from this text...",
            cancellationToken);

        var questions = new List<McqImportQuestionDto>();
        var questionKeys = new HashSet<string>(StringComparer.Ordinal);

        // Cloze items preserve a complete source sentence and blank one term found in it.
        foreach (var statement in statements)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (questions.Count >= MaximumQuestionCount)
            {
                break;
            }

            foreach (var term in terms.OrderByDescending(value => value.Length))
            {
                if (questions.Count >= MaximumQuestionCount)
                {
                    break;
                }

                if (!TryFindWholeTerm(statement.Text, term, out var termMatch))
                {
                    continue;
                }

                var clozeStatement = statement.Text[..termMatch.Index]
                    + "________"
                    + statement.Text[(termMatch.Index + termMatch.Length)..];

                var questionText =
                    "According to the chapter, complete this statement: " + clozeStatement;

                TryAddQuestion(
                    questions,
                    questionKeys,
                    questionText,
                    term,
                    terms,
                    statement.LineNumber);
            }

            if (questions.Count > 0 && questions.Count % 5 == 0)
            {
                await ReportAsync(
                    reportProgress,
                    "Built " + questions.Count + " distinct source-grounded questions so far...",
                    cancellationToken);
            }
        }

        // Definition-style items use descriptions extracted from the source and chapter terms as choices.
        foreach (var fact in facts)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (questions.Count >= MaximumQuestionCount)
            {
                break;
            }

            if (fact.Predicate.Length < 12
                || ContainsWholeTerm(fact.Predicate, fact.Subject))
            {
                continue;
            }

            var questionText =
                "Which chapter term matches this description: " + fact.Predicate.Trim() + "?";

            TryAddQuestion(
                questions,
                questionKeys,
                questionText,
                fact.Subject,
                terms,
                fact.LineNumber);
        }

        // Fallback items use only descriptions extracted from other source facts.
        var predicates = facts
            .Select(fact => fact.Predicate.Trim())
            .Where(value => value.Length >= 12 && value.Length <= 180)
            .DistinctBy(NormalizeKey)
            .ToList();

        foreach (var fact in facts)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (questions.Count >= MaximumQuestionCount)
            {
                break;
            }

            var questionText =
                "According to the chapter, which description matches the statement about " +
                fact.Subject + "?";

            TryAddQuestion(
                questions,
                questionKeys,
                questionText,
                fact.Predicate,
                predicates,
                fact.LineNumber);
        }

        if (questions.Count == 0)
        {
            throw new InvalidOperationException(
                "The local analyzer could not build any distinct, source-grounded MCQs. Upload fuller chapter notes with complete statements and distinct concepts. No MCQs were saved.");
        }

        await ReportAsync(
            reportProgress,
            "Created " + questions.Count + " questions from extracted chapter statements and terms. Checking question and option uniqueness...",
            cancellationToken);

        ValidateGeneratedQuestions(questions);

        await ReportAsync(
            reportProgress,
            "Local analysis is complete. The " + questions.Count + "-question preview is ready; nothing has been saved yet.",
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
            var parts = SentenceBoundaryPattern.Split(line);

            foreach (var part in parts)
            {
                var text = WhitespacePattern.Replace(part, " ").Trim().Trim('"', '“', '”');

                if (text.Length < 35 || text.Length > 300)
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
        var factKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var statement in statements)
        {
            var match = SubjectRelationPattern.Match(statement.Text);
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
            if (factKeys.Add(key))
            {
                facts.Add(new SourceFact(subject, predicate, statement.LineNumber));
            }
        }

        return facts;
    }

    private static List<string> ExtractTerms(
        string chapterTitle,
        string sourceText,
        IReadOnlyList<SourceFact> facts)
    {
        var terms = new List<string>();
        var knownTerms = new HashSet<string>(StringComparer.Ordinal);

        AddTerm(chapterTitle, terms, knownTerms);

        foreach (var fact in facts)
        {
            AddTerm(fact.Subject, terms, knownTerms);
        }

        foreach (Match match in QuotedTermPattern.Matches(sourceText))
        {
            AddTerm(match.Groups["term"].Value, terms, knownTerms);
        }

        foreach (Match match in AcronymPattern.Matches(sourceText))
        {
            AddTerm(match.Value, terms, knownTerms);
        }

        foreach (Match match in MultiWordTermPattern.Matches(sourceText))
        {
            AddTerm(match.Value, terms, knownTerms);
        }

        foreach (Match match in CapitalizedWordPattern.Matches(sourceText))
        {
            AddTerm(match.Value, terms, knownTerms);
        }

        // Repeated content words support ordinary explanatory notes that do not capitalize every concept.
        // Restrict the fallback to frequent non-stopwords so function words do not dominate answer choices.
        var frequentTerms = Regex.Matches(
                sourceText,
                @"\b[A-Za-z][A-Za-z0-9+#./_-]{3,}\b")
            .Select(match => match.Value)
            .Select(CleanTerm)
            .Where(IsUsefulTerm)
            .GroupBy(NormalizeKey, StringComparer.Ordinal)
            .Select(group => new
            {
                Term = group.First(),
                Frequency = group.Count()
            })
            .Where(item => item.Frequency >= 2)
            .OrderByDescending(item => item.Frequency)
            .ThenBy(item => item.Term, StringComparer.OrdinalIgnoreCase)
            .Take(50);

        foreach (var candidate in frequentTerms)
        {
            AddTerm(candidate.Term, terms, knownTerms);
        }

        return terms;
    }

    private static void AddTerm(
        string? candidate,
        ICollection<string> terms,
        ISet<string> knownTerms)
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

        var key = NormalizeKey(term);
        if (knownTerms.Add(key))
        {
            terms.Add(term);
        }
    }

    private static string CleanTerm(string value)
    {
        var term = value.Trim().Trim('"', '“', '”', ',', ';', ':', ' ');
        term = LeadingNoisePattern.Replace(term, string.Empty);
        return WhitespacePattern.Replace(term, " ").Trim().TrimEnd(',', ';', ':', '.');
    }

    private static bool IsUsefulTerm(string term)
    {
        if (term.Length < 2 || term.Length > 60 || StopTerms.Contains(NormalizeKey(term)))
        {
            return false;
        }

        var wordCount = term.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        if (wordCount > 5)
        {
            return false;
        }

        var firstWord = term.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        return !StopTerms.Contains(NormalizeKey(firstWord));
    }

    private static bool TryAddQuestion(
        ICollection<McqImportQuestionDto> questions,
        ISet<string> questionKeys,
        string questionText,
        string correctAnswer,
        IReadOnlyList<string> optionPool,
        int sourceLine)
    {
        if (string.IsNullOrWhiteSpace(questionText)
            || string.IsNullOrWhiteSpace(correctAnswer)
            || !questionKeys.Add(NormalizeKey(questionText)))
        {
            return false;
        }

        var optionValues = BuildOptions(correctAnswer, optionPool, questions.Count);
        if (optionValues is null)
        {
            questionKeys.Remove(NormalizeKey(questionText));
            return false;
        }

        var correctNumber = optionValues.FindIndex(
            option => string.Equals(
                NormalizeKey(option),
                NormalizeKey(correctAnswer),
                StringComparison.Ordinal));

        if (correctNumber < 0)
        {
            questionKeys.Remove(NormalizeKey(questionText));
            return false;
        }

        questions.Add(new McqImportQuestionDto
        {
            Number = questions.Count + 1,
            SourceLine = Math.Max(0, sourceLine),
            Question = questionText.Trim(),
            Option1 = optionValues[0],
            Option2 = optionValues[1],
            Option3 = optionValues[2],
            Option4 = optionValues[3],
            CorrectOptionNumber = correctNumber + 1
        });

        return true;
    }

    private static List<string>? BuildOptions(
        string correctAnswer,
        IReadOnlyList<string> optionPool,
        int questionIndex)
    {
        var normalizedAnswer = NormalizeKey(correctAnswer);
        var candidates = optionPool
            .Select(value => WhitespacePattern.Replace(value.Trim(), " ").Trim().Trim('"', '“', '”'))
            .Where(value =>
                value.Length > 0
                && value.Length <= 180
                && !string.Equals(NormalizeKey(value), normalizedAnswer, StringComparison.Ordinal))
            .DistinctBy(NormalizeKey)
            .ToList();

        if (candidates.Count < 3)
        {
            return null;
        }

        var selectedDistractors = new List<string>(3);
        var startIndex = (questionIndex * 7 + 3) % candidates.Count;

        for (var offset = 0; offset < candidates.Count && selectedDistractors.Count < 3; offset++)
        {
            var candidate = candidates[(startIndex + offset) % candidates.Count];
            if (string.Equals(NormalizeKey(candidate), normalizedAnswer, StringComparison.Ordinal)
                || selectedDistractors.Any(selected =>
                    string.Equals(NormalizeKey(selected), NormalizeKey(candidate), StringComparison.Ordinal)))
            {
                continue;
            }

            selectedDistractors.Add(candidate);
        }

        if (selectedDistractors.Count != 3)
        {
            return null;
        }

        var options = new List<string>(4);
        var correctPosition = questionIndex % 4;
        var distractorIndex = 0;

        for (var position = 0; position < 4; position++)
        {
            options.Add(position == correctPosition
                ? correctAnswer.Trim()
                : selectedDistractors[distractorIndex++]);
        }

        return options;
    }

    private static void ValidateGeneratedQuestions(IReadOnlyList<McqImportQuestionDto> questions)
    {
        if (questions.Count == 0 || questions.Count > MaximumQuestionCount)
        {
            throw new InvalidOperationException(
                "Local analysis must produce between 1 and 100 questions. Nothing was saved.");
        }

        var normalizedQuestions = new HashSet<string>(StringComparer.Ordinal);
        foreach (var question in questions)
        {
            var options = new[]
            {
                question.Option1,
                question.Option2,
                question.Option3,
                question.Option4
            };

            if (string.IsNullOrWhiteSpace(question.Question)
                || options.Any(string.IsNullOrWhiteSpace)
                || question.CorrectOptionNumber is < 1 or > 4
                || options.Select(NormalizeKey).Distinct(StringComparer.Ordinal).Count() != 4)
            {
                throw new InvalidOperationException(
                    "The local analyzer produced a question with incomplete or repeated options. Nothing was saved; upload clearer chapter notes and try again.");
            }

            if (!normalizedQuestions.Add(NormalizeKey(question.Question)))
            {
                throw new InvalidOperationException(
                    "The local analyzer produced duplicate questions. Nothing was saved; upload fuller chapter notes and try again.");
            }
        }
    }

    private static bool ContainsWholeTerm(string text, string term)
    {
        return TryFindWholeTerm(text, term, out _);
    }

    private static bool TryFindWholeTerm(string text, string term, out Match match)
    {
        var pattern = $@"(?<![\p{{L}}\p{{N}}_]){Regex.Escape(term)}(?![\p{{L}}\p{{N}}_])";
        match = Regex.Match(
            text,
            pattern,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return match.Success;
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

    private sealed record SourceFact(string Subject, string Predicate, int LineNumber);
}
