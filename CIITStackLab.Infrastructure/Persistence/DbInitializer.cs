using CIITStackLab.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CIITStackLab.Infrastructure.Persistence;

public static class DbInitializer
{
    public static async Task SeedAsync(ApplicationDbContext dbContext)
    {
        await dbContext.Database.MigrateAsync();

        if (await dbContext.Courses.AnyAsync())
        {
            return;
        }

        var courses = new[]
        {
            new Course
            {
                Title = "C# Programming",
                ShortDescription = "Build a strong foundation in modern C# programming.",
                Description = "Learn C# fundamentals, object-oriented programming, collections, exception handling and practical application development.",
                ImageUrl = "/Course_img/b0998bd1-f159-4388-94ea-66a4be8d1ab2.png",
                Category = "Programming",
                Level = "Beginner",
                DurationHours = 40,
                IsPublished = true
            },
            new Course
            {
                Title = "Python Full Stack",
                ShortDescription = "Learn Python from fundamentals to full-stack development.",
                Description = "Build Python applications and progress toward full-stack development with practical projects.",
                ImageUrl = "/Course_img/Python_full_stack.png",
                Category = "Full Stack",
                Level = "Intermediate",
                DurationHours = 60,
                IsPublished = true
            },
            new Course
            {
                Title = ".NET Full Stack",
                ShortDescription = "Master ASP.NET Core and modern full-stack .NET development.",
                Description = "Learn C#, ASP.NET Core MVC, Entity Framework Core, APIs and full-stack application development.",
                ImageUrl = "/Course_img/DotNet_Full_Stack.png",
                Category = "Full Stack",
                Level = "Intermediate",
                DurationHours = 60,
                IsPublished = true
            },
            new Course
            {
                Title = "DevOps",
                ShortDescription = "Understand CI/CD, containers, cloud and deployment workflows.",
                Description = "Learn practical DevOps concepts including source control, CI/CD, containers and deployment automation.",
                ImageUrl = "/Course_img/DevOps.png",
                Category = "DevOps",
                Level = "Intermediate",
                DurationHours = 45,
                IsPublished = true
            },
            new Course
            {
                Title = "Data Analytics",
                ShortDescription = "Turn data into useful insights with practical analytics.",
                Description = "Learn data preparation, analysis, visualization and the core workflow used by data analysts.",
                ImageUrl = "/Course_img/Data_Analytics.png",
                Category = "Data",
                Level = "Beginner",
                DurationHours = 40,
                IsPublished = true
            },
            new Course
            {
                Title = "Data Science",
                ShortDescription = "Learn the foundations of data science and machine learning.",
                Description = "Explore data science workflows, statistics, machine learning concepts and practical projects.",
                ImageUrl = "/Course_img/Data_Science.png",
                Category = "Data",
                Level = "Intermediate",
                DurationHours = 55,
                IsPublished = true
            },
            new Course
            {
                Title = "Java Programming",
                ShortDescription = "Build strong Java programming and OOP fundamentals.",
                Description = "Learn Java syntax, object-oriented programming, collections, exceptions and application development.",
                ImageUrl = "/Course_img/748ca555-f359-43c7-838a-4104184ec450.png",
                Category = "Programming",
                Level = "Beginner",
                DurationHours = 40,
                IsPublished = true
            },
            new Course
            {
                Title = "Web Development",
                ShortDescription = "Learn the fundamentals of modern web development.",
                Description = "Build responsive websites and understand the essential technologies behind the modern web.",
                ImageUrl = "/Course_img/5ae4054a-8bdd-4b84-b913-31a4c879c99f.png",
                Category = "Web Development",
                Level = "Beginner",
                DurationHours = 35,
                IsPublished = true
            },
            new Course
            {
                Title = "Full Stack with DevOps",
                ShortDescription = "Combine full-stack development with deployment and DevOps practices.",
                Description = "Build complete applications and learn the development-to-deployment workflow using modern DevOps practices.",
                ImageUrl = "/Course_img/Full_Stack_With_DevOps.png",
                Category = "Full Stack",
                Level = "Advanced",
                DurationHours = 75,
                IsPublished = true
            }
        };

        await dbContext.Courses.AddRangeAsync(courses);
        await dbContext.SaveChangesAsync();
    }
}