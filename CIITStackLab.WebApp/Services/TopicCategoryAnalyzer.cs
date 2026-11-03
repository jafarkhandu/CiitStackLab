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

    // Technology aliases and Devicon slugs live in one place so topic cards do not
    // need their own icon switches. The generated fallback below covers new simple
    // technology names when Devicon follows its normal naming convention.
    private static readonly (string[] Aliases, string Slug)[] TechnologyIcons =
    [
        (["html", "html5"], "html5"),
        (["css", "css3"], "css3"),
        (["javascript", "js"], "javascript"),
        (["jquery"], "jquery"),
        (["bootstrap"], "bootstrap"),
        (["react"], "react"),
        (["typescript"], "typescript"),
        (["angular", "angularjs"], "angularjs"),
        (["vue", "vue.js", "vuejs"], "vuejs"),
        (["svelte"], "svelte"),
        (["node", "node.js", "nodejs"], "nodejs"),
        (["python"], "python"),
        (["ruby"], "ruby"),
        (["php"], "php"),
        (["java", "jdbc", "jsp", "servlet"], "java"),
        (["c#", "c sharp"], "csharp"),
        (["c++", "cplusplus"], "cplusplus"),
        (["kotlin"], "kotlin"),
        (["swift"], "swift"),
        (["rust"], "rust"),
        (["golang", "go language"], "go"),
        (["oracle", "pl/sql"], "oracle"),
        (["mysql"], "mysql"),
        (["mongodb", "mongo db"], "mongodb"),
        (["sql server", "mssql", "microsoft sql server"], "microsoftsqlserver"),
        (["postgres", "postgresql"], "postgresql"),
        (["redis"], "redis"),
        (["sqlite"], "sqlite"),
        (["cassandra"], "cassandra"),
        (["dynamodb", "dynamo db"], "dynamodb"),
        (["hibernate"], "hibernate"),
        (["spring", "spring framework", "spring boot", "spring mvc"], "spring"),
        (["maven"], "maven"),
        (["gradle"], "gradle"),
        (["django"], "django"),
        (["flask"], "flask"),
        (["fastapi", "fast api"], "fastapi"),
        (["express", "express.js", "expressjs"], "express"),
        (["jenkins"], "jenkins"),
        (["azure"], "azure"),
        (["aws", "amazon web services"], "amazonwebservices"),
        (["docker"], "docker"),
        (["kubernetes", "k8s"], "kubernetes"),
        (["terraform"], "terraform"),
        (["ansible"], "ansible"),
        (["linux"], "linux"),
        (["github"], "github"),
        (["gitlab"], "gitlab"),
        (["git"], "git"),
        (["selenium"], "selenium"),
        (["playwright"], "playwright"),
        (["cypress"], "cypress"),
        (["junit"], "junit"),
        (["xunit"], "dotnet"),
        (["pandas"], "pandas"),
        (["numpy", "numPy"], "numpy"),
        (["matplotlib"], "matplotlib"),
        (["dotnet", ".net", ".net core", "asp.net", "asp.net core"], "dotnetcore")
    ];

    private static readonly HashSet<string> NonTechnologyTopics = new(StringComparer.Ordinal)
    {
        "frontend",
        "front-end",
        "backend",
        "back-end",
        "api",
        "rest",
        "graphql",
        "programming fundamentals",
        "data structures",
        "algorithms",
        "oop",
        "database",
        "sql",
        "nosql",
        "testing",
        "qa",
        "quality assurance",
        "devops",
        "cloud computing",
        "deployment",
        "monitoring",
        "security",
        "networking",
        "cybersecurity",
        "machine learning",
        "deep learning",
        "artificial intelligence",
        "data science",
        "data analytics",
        "statistics",
        "generative ai",
        "llm"
    };

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

    /// <summary>
    /// Resolves the technology icon used by topic cards from the same normalized
    /// topic title used by the category analyzer. Keeping this mapping here prevents
    /// course views from maintaining a second, divergent icon resolver.
    /// </summary>
    public static string GetIconClass(string? topicTitle)
    {
        var title = Normalize(topicTitle);
        if (title.Length == 0)
        {
            return "bi bi-code-slash";
        }

        foreach (var (aliases, slug) in TechnologyIcons)
        {
            if (aliases.Any(alias => ContainsPhrase(title, Normalize(alias))))
            {
                return BuildDeviconClass(slug);
            }
        }

        // For a new, simple technology title such as "Ruby", "Rust" or another
        // Devicon-supported technology, derive the standard Devicon slug instead of
        // immediately falling back to the generic </> icon. Explicit aliases above
        // handle names whose Devicon slug differs from their display name.
        if (!NonTechnologyTopics.Contains(title) && IsSimpleTechnologyTitle(title))
        {
            var generatedSlug = ToDeviconSlug(title);
            if (generatedSlug.Length > 0)
            {
                return BuildDeviconClass(generatedSlug);
            }
        }

        return "bi bi-code-slash";
    }

    private static string BuildDeviconClass(string slug)
        => $"devicon-{slug}-plain colored";

    private static bool IsSimpleTechnologyTitle(string title)
        => title.Length <= 40
           && Regex.IsMatch(title, @"^[a-z0-9][a-z0-9 .+#/-]*$")
           && title.Any(char.IsLetter);

    private static string ToDeviconSlug(string title)
    {
        var slug = title
            .Replace(".js", "js", StringComparison.Ordinal)
            .Replace(".net", "net", StringComparison.Ordinal)
            .Replace("#", "sharp", StringComparison.Ordinal)
            .Replace("+", "plus", StringComparison.Ordinal);

        slug = Regex.Replace(slug, @"[^a-z0-9]+", "-").Trim('-');
        return slug;
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
