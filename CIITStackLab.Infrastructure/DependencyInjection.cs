using CIITStackLab.Application.Interfaces;
using CIITStackLab.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using CIITStackLab.Infrastructure.Persistence;
using CIITStackLab.Infrastructure.Payments;
using CIITStackLab.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CIITStackLab.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection is not configured.");

        services.AddMemoryCache();
        services.AddHttpClient();

            services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.EnableRetryOnFailure();
                sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "erpsystem");
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

        services.Configure<RazorpayOptions>(configuration.GetSection("Razorpay"));
        services.AddSingleton<PhonePePaymentService>();

        services.AddScoped<CourseService>();
        services.AddScoped<ICourseService, DynamicCourseService>();
        services.AddScoped<IAdminDashboardService, AdminDashboardService>();
        services.AddScoped<IAdminCourseService, AdminCourseService>();
        services.AddScoped<IAdminTopicService, AdminTopicService>();
        services.AddScoped<IAdminCourseTopicMappingService, AdminCourseTopicMappingService>();
        services.AddScoped<IAdminContentService, AdminContentService>();
        services.AddScoped<IMuxVideoService, MuxVideoService>();
        services.AddScoped<IVimeoVideoService, VimeoVideoService>();
        services.AddScoped<IAdminMcqService, AdminMcqService>();
        services.AddScoped<IAdminInterviewQuestionService, AdminInterviewQuestionService>();
        services.AddScoped<IInterviewQuestionAnalyzerService, InterviewQuestionAnalyzerService>();
        services.AddScoped<IInterviewQuestionImportService, InterviewQuestionImportService>();
        services.AddScoped<IAdminPracticeProgramService, AdminPracticeProgramService>();
        services.AddScoped<IPracticeProgramImportService, PracticeProgramImportService>();
        services.AddScoped<INotesService, NotesService>();
        services.AddScoped<IChapterMcqAnalyzerService, ChapterMcqAnalyzerService>();
        services.AddScoped<IMcqImportService, McqImportService>();
        services.AddScoped<IStudentProgressService, StudentProgressService>();
        services.AddScoped<ICourseAssessmentService, CourseAssessmentService>();
        services.AddScoped<TopicExaminationService>();
        services.AddScoped<ITopicExaminationService, HatimExamApplicationService>();
        services.AddHostedService<TopicExamExpiryWorker>();
        services.AddScoped<CoursePurchaseService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<PaymentNotificationService>();
        services.AddScoped<IAdminStudentService, AdminStudentService>();
        services.AddScoped<IAdminSettingsService, AdminSettingsService>();
        services.AddScoped<IAdminReportsService, AdminReportsService>();
        services.AddScoped<IPhonePeAdminReportsService, PhonePeAdminReportsService>();

        return services;
    }
}
