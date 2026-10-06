using CIITStackLab.Infrastructure;
using CIITStackLab.Infrastructure.Persistence;
using CIITStackLab.Infrastructure.Identity;

var builder = WebApplication.CreateBuilder(args);

// Add MVC services
builder.Services.AddControllersWithViews();

// Add application infrastructure
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// Apply database migrations and seed initial course data.
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await DbInitializer.SeedAsync(dbContext);
    await IdentitySeeder.SeedAsync(scope.ServiceProvider, app.Configuration);
}

// Configure HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();