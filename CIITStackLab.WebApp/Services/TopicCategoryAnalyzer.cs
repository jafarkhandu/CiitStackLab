using System.Text.RegularExpressions;

namespace CIITStackLab.WebApp.Services;

/// <summary>
/// Shared, deterministic topic classifier used to build learning-path compartments.
/// Unknown topics receive their own compartment rather than being incorrectly placed
/// into the generic programming category.
/// </summary>
public static class TopicCategoryAnalyzer
{
    private static readonly (string Category, string[] Keywords)[] Categories =
    [
        ("Frontend", ["html", "css", "javascript", "typescript", "jquery", "bootstrap", "react", "angular", "vue", "svelte", "frontend", "front-end", "web design", "ui/ux"]),
        ("Backend & APIs", ["backend", "back-end", "api", "rest", "graphql", "node.js", "express", "django", "flask", "fastapi", "spring boot", "asp.net", ".net core", "web api"]),
        ("Programming Languages", ["java", "python", "c#", "c++", "c sharp", "kotlin", "swift", "rust", "golang", "go language", "ruby", "php", "programming fundamentals", "data structures", "algorithms", "oop"]),
        ("Databases", ["sql", "mysql", "database", "oracle", "mongodb", "mongo db", "postgres", "postgresql", "pl/sql", "redis", "sqlite", "nosql", "cassandra", "dynamodb"]),
        ("Java Enterprise", ["jdbc", "jsp", "servlet", "j2ee", "jakarta ee", "hibernate", "spring framework", "spring mvc", "maven", "gradle"]),
        ("Data Science & AI", ["machine learning", "deep learning", "artificial intelligence", "data science", "data analytics", "pandas", "numpy", "matplotlib", "neural network", "natural language processing", "nlp", "computer vision", "generative ai", "llm", "statistics"]),
        ("Cloud & DevOps", ["git", "github", "gitlab", "jenkins", "azure", "aws", "devops", "docker", "kubernetes", "ci/cd", "terraform", "ansible", "linux", "cloud computing", "deployment", "monitoring"]),
        ("Testing & Quality", ["testing", "qa", "quality assurance", "selenium", "playwright", "cypress", "unit test", "integration test", "automation testing", "junit", "xunit"]),
        ("Cybersecurity & Networking", ["cybersecurity", "security", "ethical hacking", "penetration testing", "networking", "network security", "cryptography", "firewall", "tcp/ip"]),
        ("Business & Productivity", ["excel", "power bi", "tableau", "accounting", "marketing", "project management", "business analysis", "communication skills", "powerpoint", "word", "numPy"])
    ];

    public static string GetCategory(string? topicTitle)
    {
        var title = Normalize(topicTitle);
        if (title.Length == 0)
        {
            return "Uncategorized Topics";
        }

        foreach (var (category, keywords) in Categories)
        {
            if (keywords.Any(keyword => ContainsPhrase(title, Normalize(keyword))))
            {
                return category;
            }
        }

        // Never force an unknown subject into an unrelated existing category.
        // Its title becomes a new compartment, which can be consolidated later
        // by adding a domain rule above.
        return $"Specialized: {ToDisplayTitle(topicTitle!)}";
    }

    private static bool ContainsPhrase(string title, string keyword)
    {
        if (keyword.Length <= 3 && keyword.All(char.IsLetterOrDigit))
        {
            return Regex.IsMatch(title, $@"(?<![a-z0-9]){Regex.Escape(keyword)}(?![a-z0-9])");
        }

        return title.Contains(keyword, StringComparison.Ordinal);
    }

    private static string Normalize(string? value)
        => Regex.Replace(value?.Trim().ToLowerInvariant() ?? string.Empty, @"\s+", " ");

    private static string ToDisplayTitle(string title)
        => Regex.Replace(title.Trim(), @"\s+", " ");
}
