using CIITStackLab.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CIITStackLab.Infrastructure.Persistence;

public static class DbInitializer
{
    public static async Task SeedAsync(ApplicationDbContext dbContext)
    {
        await dbContext.Database.MigrateAsync();

        var desiredCourses = new[]
        {
            new
            {
                Title = "Java Full Stack",
                ShortDescription = "Learn Java, JDBC and many more..",
                Description = "Build Java applications with strong programming, object-oriented and full-stack development fundamentals.",
                ImageUrl = "/Course_img/java-full-stack.svg",
                Category = "Full Stack",
                Level = "Beginner",
                DurationHours = 45,
                IsPublished = true
            },
            new
            {
                Title = "Python Full Stack",
                ShortDescription = "Learn Python, Django, FastApi and many more.",
                Description = "Build Python applications and progress from programming fundamentals to full-stack development.",
                ImageUrl = "/Course_img/python-full-stack.svg",
                Category = "Full Stack",
                Level = "Intermediate",
                DurationHours = 60,
                IsPublished = true
            },
            new
            {
                Title = ".Net Full Stack",
                ShortDescription = "Build applications using C#, .NET Core and many more.",
                Description = "Learn C#, ASP.NET Core, Entity Framework Core and modern full-stack .NET application development.",
                ImageUrl = "/Course_img/dotnet-full-stack.svg",
                Category = "Full Stack",
                Level = "Intermediate",
                DurationHours = 60,
                IsPublished = true
            },
            new
            {
                Title = "DevOps",
                ShortDescription = "Learn Jira, Github, Jenkins, AWS, Azure and many more.",
                Description = "Learn source control, CI/CD, containers, cloud platforms and practical DevOps workflows.",
                ImageUrl = "/Course_img/devops.svg",
                Category = "DevOps",
                Level = "Intermediate",
                DurationHours = 45,
                IsPublished = true
            },
            new
            {
                Title = "Data Analytics",
                ShortDescription = "Learn python, RDBMS, Excel, PowerBI and many more.",
                Description = "Learn data preparation, SQL, Excel, Python and dashboarding for practical analytics.",
                ImageUrl = "/Course_img/data-analytics.svg",
                Category = "Data",
                Level = "Beginner",
                DurationHours = 40,
                IsPublished = true
            },
            new
            {
                Title = "Data Science",
                ShortDescription = "Learn python, AI, ML, Gen AI, LLM, RAG, Agentic AI and many more.",
                Description = "Explore statistics, machine learning, generative AI and practical data science workflows.",
                ImageUrl = "/Course_img/data-science.svg",
                Category = "Data",
                Level = "Intermediate",
                DurationHours = 55,
                IsPublished = true
            },
            new
            {
                Title = "Software Testing",
                ShortDescription = "Learn Core Java, Manual Testing, Selenium, Playwright and many more.",
                Description = "Learn manual testing and automation with Java, Selenium and Playwright.",
                ImageUrl = "/Course_img/software-testing.svg",
                Category = "Testing",
                Level = "Beginner",
                DurationHours = 45,
                IsPublished = true
            },
            new
            {
                Title = "DevOps with Gen AI",
                ShortDescription = "Learn Azure, AWS DevOps, Gen AI and many more.",
                Description = "Combine cloud DevOps practices with modern generative AI workflows and automation.",
                ImageUrl = "/Course_img/devops-genai.svg",
                Category = "DevOps",
                Level = "Advanced",
                DurationHours = 55,
                IsPublished = true
            },
            new
            {
                Title = "Full Stack With DevOps",
                ShortDescription = "Learn Full Stack, Azure, AWS DevOps, Gen AI and many more.",
                Description = "Learn complete full-stack development together with cloud, DevOps and generative AI practices.",
                ImageUrl = "/Course_img/full-stack-devops.svg",
                Category = "Full Stack",
                Level = "Advanced",
                DurationHours = 75,
                IsPublished = true
            }
        };

        var courses = await dbContext.Courses.OrderBy(x => x.Id).ToListAsync();

        if (courses.Count == 0)
        {
            foreach (var desired in desiredCourses)
            {
                await dbContext.Courses.AddAsync(new Course
                {
                    Title = desired.Title,
                    ShortDescription = desired.ShortDescription,
                    Description = desired.Description,
                    ImageUrl = desired.ImageUrl,
                    Category = desired.Category,
                    Level = desired.Level,
                    DurationHours = desired.DurationHours,
                    IsPublished = desired.IsPublished
                });
            }

            await dbContext.SaveChangesAsync();
            return;
        }

        for (var i = 0; i < Math.Min(courses.Count, desiredCourses.Length); i++)
        {
            var existing = courses[i];
            var desired = desiredCourses[i];

            existing.Title = desired.Title;
            existing.ShortDescription = desired.ShortDescription;
            existing.Description = desired.Description;
            existing.ImageUrl = desired.ImageUrl;
            existing.Category = desired.Category;
            existing.Level = desired.Level;
            existing.DurationHours = desired.DurationHours;
            existing.IsPublished = desired.IsPublished;
        }

        if (courses.Count < desiredCourses.Length)
        {
            for (var i = courses.Count; i < desiredCourses.Length; i++)
            {
                var desired = desiredCourses[i];

                await dbContext.Courses.AddAsync(new Course
                {
                    Title = desired.Title,
                    ShortDescription = desired.ShortDescription,
                    Description = desired.Description,
                    ImageUrl = desired.ImageUrl,
                    Category = desired.Category,
                    Level = desired.Level,
                    DurationHours = desired.DurationHours,
                    IsPublished = desired.IsPublished
                });
            }
        }

        await dbContext.SaveChangesAsync();
    }
}