using CIITStackLab.Application.Interfaces;
using CIITStackLab.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using CIITStackLab.Infrastructure.Persistence;
using CIITStackLab.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CIITStackLab.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection is not configured.");

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.EnableRetryOnFailure();
            }));

        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
        {
            options.Password.RequiredLength = 8;
            options.Password.RequireDigit = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.User.RequireUniqueEmail = true;
            options.SignIn.RequireConfirmedAccount = false;
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

        services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/Account/Login";
            options.AccessDeniedPath = "/Account/AccessDenied";
            options.ExpireTimeSpan = TimeSpan.FromDays(14);
            options.SlidingExpiration = true;
        });

        services.AddScoped<CourseService>();
        services.AddScoped<ICourseService, DynamicCourseService>();
        services.AddScoped<IAdminDashboardService, AdminDashboardService>();
        services.AddScoped<IAdminCourseService, AdminCourseService>();
        services.AddScoped<IAdminTopicService, AdminTopicService>();
        services.AddScoped<IAdminCourseTopicMappingService, AdminCourseTopicMappingService>();
        services.AddScoped<IAdminContentService, AdminContentService>();
        services.AddScoped<INotesService, NotesService>();
        services.AddScoped<IStudentProgressService, StudentProgressService>();
        services.AddScoped<ICourseAssessmentService, CourseAssessmentService>();
        services.AddScoped<ICourseEnrollmentService, CourseEnrollmentService>();
        services.AddScoped<IAdminStudentService, AdminStudentService>();
        services.AddScoped<IAdminSettingsService, AdminSettingsService>();

        return services;
    }
}
