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

        return title switch
        {
            var value when value.Contains("html") => "devicon-html5-plain colored",
            var value when value.Contains("css") => "devicon-css3-plain colored",
            var value when value.Contains("javascript") || value == "js" => "devicon-javascript-plain colored",
            var value when value.Contains("jquery") => "devicon-jquery-plain colored",
            var value when value.Contains("bootstrap") => "devicon-bootstrap-plain colored",
            var value when value.Contains("react") => "devicon-react-original colored",
            var value when value.Contains("typescript") => "devicon-typescript-plain colored",
            var value when value.Contains("angular") => "devicon-angularjs-plain colored",
            var value when value.Contains("node") => "devicon-nodejs-plain colored",
            var value when value.Contains("python") => "devicon-python-plain colored",
            var value when value.Contains("oracle") || value.Contains("pl/sql") => "devicon-oracle-original colored",
            var value when value.Contains("mysql") => "devicon-mysql-original colored",
            var value when value.Contains("mongodb") || value.Contains("mongo db") => "devicon-mongodb-plain colored",
            var value when value.Contains("sql server") || value.Contains("mssql") => "devicon-microsoftsqlserver-plain colored",
            var value when value.Contains("hibernate") => "devicon-hibernate-plain colored",
            var value when value.Contains("spring") => "devicon-spring-original colored",
            var value when value.Contains("java") || value.Contains("jdbc") || value.Contains("jsp") || value.Contains("servlet") => "devicon-java-plain colored",
            var value when value.Contains("jenkins") => "devicon-jenkins-plain colored",
            var value when value.Contains("azure") => "devicon-azure-plain colored",
            var value when value.Contains("aws") || value.Contains("amazon web services") => "devicon-amazonwebservices-plain-wordmark colored",
            var value when value.Contains("docker") => "devicon-docker-plain colored",
            var value when value.Contains("github") => "devicon-github-original colored",
            var value when value.Contains("git") => "devicon-git-plain colored",
            var value when value.Contains("c#") || value.Contains(".net") => "devicon-dotnetcore-plain colored",
            var value when value.Contains("sql") || value.Contains("database") => "bi bi-database",
            _ => "bi bi-code-slash"
        };
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
