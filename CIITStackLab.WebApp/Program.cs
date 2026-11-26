using CIITStackLab.Infrastructure;
using CIITStackLab.Infrastructure.Identity;
using CIITStackLab.Infrastructure.Persistence;
using CIITStackLab.WebApp.Hubs;
using CIITStackLab.WebApp.Services;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.StaticFiles;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddJsonFile(
        "appsettings.Local.json",
        optional: true,
        reloadOnChange: true);
}

builder.Services.AddControllersWithViews();
builder.Services.Configure<Microsoft.AspNetCore.Mvc.Razor.RazorViewEngineOptions>(options =>
{
    options.ViewLocationFormats.Insert(0, "/Areas/Student/Views/{1}/{0}.cshtml");
    options.ViewLocationFormats.Insert(1, "/Areas/Student/Views/Shared/{0}.cshtml");
});
builder.Services.AddSignalR();
builder.Services.AddScoped<ExamApplicationNotificationService>();
builder.Services.AddScoped<MeetRealtimeService>();
builder.Services.AddHttpClient<MeetIceServerService>(client =>
{
    client.BaseAddress = new Uri("https://rtc.live.cloudflare.com/");
    client.Timeout = TimeSpan.FromSeconds(3);
    client.MaxResponseContentBufferSize = 16 * 1024;
})
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false })
    .RemoveAllLoggers();
builder.Services.AddSingleton<MeetNotificationService>();
builder.Services.AddHostedService<MeetSessionLifecycleService>();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(15);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await DbInitializer.VerifyAsync(dbContext);
    await IdentitySeeder.VerifyAsync(scope.ServiceProvider);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// A browser navigation can abort an in-flight request. Treat only cancellations
// caused by RequestAborted as expected, without hiding unrelated application errors.
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
    {
        // The client has already abandoned this request; do not turn it into an application error.
    }
});

app.UseHttpsRedirection();

var contentTypeProvider = new FileExtensionContentTypeProvider();
contentTypeProvider.Mappings[".tflite"] = "application/octet-stream";

app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = contentTypeProvider,
    OnPrepareResponse = static asset =>
    {
        var request = asset.Context.Request;
        if (!request.Path.StartsWithSegments("/css")
            && !request.Path.StartsWithSegments("/js")
            && !request.Path.StartsWithSegments("/lib"))
            return;

        if (!request.Query.TryGetValue("v", out var versions) || versions.Count != 1 || versions[0]?.Length != 43)
            return;

        var versionProvider = asset.Context.RequestServices.GetRequiredService<IFileVersionProvider>();
        var versionedPath = versionProvider.AddFileVersionToPath(request.PathBase, request.Path);
        if (versionedPath.EndsWith($"?v={versions[0]}", StringComparison.Ordinal))
            asset.Context.Response.Headers.CacheControl = "public,max-age=31536000,immutable";
    }
});

app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHub<ExamApplicationNotificationHub>("/hubs/exam-application-notifications");
app.MapHub<MeetHub>("/hubs/meet");

app.Run();
