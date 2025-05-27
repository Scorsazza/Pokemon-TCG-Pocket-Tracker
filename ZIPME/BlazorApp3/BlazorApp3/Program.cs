using BlazorApp3.Client.Pages;
using BlazorApp3.Components;
using BlazorApp3.Components.Account;
using BlazorApp3.Data;
using BlazorApp3.Data.Models;
using BlazorApp3.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

var builder = WebApplication.CreateBuilder(args);

// 1) Blazor WASM components & authentication state
builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents()
    .AddInteractiveServerComponents()
    .AddAuthenticationStateSerialization();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityUserAccessor>();
builder.Services.AddScoped<IdentityRedirectManager>();

// 2) Identity cookie schemes
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = IdentityConstants.ApplicationScheme;
    options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
})
.AddIdentityCookies();

builder.Services.AddAuthorization();

// 3) EF Core + Identity stores - CHANGED TO SQLITE
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                       ?? "Data Source=app.db"; // Default SQLite connection string
builder.Services.AddDbContext<ApplicationDbContext>(opts =>
    opts.UseSqlite(connectionString)); // Changed from UseSqlServer to UseSqlite
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentityCore<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.User.RequireUniqueEmail = false;
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
    options.Password.RequiredUniqueChars = 1;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddSignInManager()
.AddDefaultTokenProviders();

// 4) Register IHttpClientFactory so the service can inject it
builder.Services.AddHttpClient();

// 5) Application service
builder.Services.AddScoped<IPokemonCollectionService, PokemonCollectionService>();

// 6) MVC Controllers (disable antiforgery for API)
builder.Services.AddControllers(options =>
{
    options.Filters.Add(new IgnoreAntiforgeryTokenAttribute());
});

// 7) Add Antiforgery services
builder.Services.AddAntiforgery();

// 8) Caching & memory
builder.Services.AddResponseCaching();
builder.Services.AddMemoryCache();

var app = builder.Build();

// --- Middleware pipeline ---

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// ROUTING
app.UseRouting();

// AUTHENTICATION & AUTHORIZATION
app.UseAuthentication();
app.UseAuthorization();

// ANTIFORGERY - Must come after UseAuthentication() and UseAuthorization()
app.UseAntiforgery();

app.UseResponseCaching();

// Endpoints
app.MapControllers();
app.MapAdditionalIdentityEndpoints();
app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode()
   .AddInteractiveWebAssemblyRenderMode()
   .AddAdditionalAssemblies(typeof(BlazorApp3.Client._Imports).Assembly);

// Simple test
app.MapGet("/api/test", () => "API works!");

// Ensure DB & seed - CHANGED TO USE MIGRATE INSTEAD OF ENSURECREATED
using (var scope = app.Services.CreateScope())
{
    var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var um = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

    // Use Migrate() instead of EnsureCreated() for better SQLite support
    await ctx.Database.MigrateAsync();
    await SeedInitialData(ctx, um);
}

app.Run();

static async Task SeedInitialData(
    ApplicationDbContext context,
    UserManager<ApplicationUser> userManager)
{
    var admin = await userManager.FindByNameAsync("admin");
    if (admin == null)
    {
        admin = new ApplicationUser { UserName = "admin" };
        var res = await userManager.CreateAsync(admin, "Admin123!");
        if (res.Succeeded)
        {
            context.UserStats.Add(new UserStats
            {
                UserId = admin.Id,
                TotalCards = 0,
                UniqueCards = 0,
                CompletedSets = 0,
                BountiesCompleted = 0,
                BountiesPosted = 0,
                TradesCompleted = 0,
                LastUpdated = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
        }
    }
}