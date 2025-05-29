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
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

var builder = WebApplication.CreateBuilder(args);

// --- Blazor Server-Interactive + Auth state ---
builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents()
    .AddInteractiveServerComponents()
    .AddAuthenticationStateSerialization();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityUserAccessor>();
builder.Services.AddScoped<IdentityRedirectManager>();

// --- CRUCIAL: HttpClient that preserves cookies ---
builder.Services.AddScoped(sp =>
{
    var nav = sp.GetRequiredService<NavigationManager>();
    var handler = new HttpClientHandler
    {
        UseCookies = true,
        CookieContainer = new System.Net.CookieContainer()
    };
    var client = new HttpClient(handler)
    {
        BaseAddress = new Uri(nav.BaseUri)
    };
    // ensure JSON
    client.DefaultRequestHeaders.Accept.Add(
        new MediaTypeWithQualityHeaderValue("application/json"));
    return client;
});

// --- Identity cookie schemes ---
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = IdentityConstants.ApplicationScheme;
    options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
})
.AddIdentityCookies();

builder.Services.AddAuthorization();

// --- EF Core & Identity stores (SQLite) ---
var connectionString = builder.Configuration
    .GetConnectionString("DefaultConnection")
    ?? "Data Source=app.db";
builder.Services.AddDbContext<ApplicationDbContext>(opts =>
    opts.UseSqlite(connectionString));
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
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddSignInManager()
.AddDefaultTokenProviders();

// --- App services & controllers ---
builder.Services.AddHttpClient();
builder.Services.AddScoped<IPokemonCollectionService, PokemonCollectionService>();
builder.Services.AddControllers(options =>
{
    options.Filters.Add(new IgnoreAntiforgeryTokenAttribute());
});
builder.Services.AddAntiforgery();
builder.Services.AddResponseCaching();
builder.Services.AddMemoryCache();

var app = builder.Build();

// --- Middleware ---
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
app.UseRouting();

// Auth must come before MapControllers
app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();
app.UseResponseCaching();

// --- Endpoints ---
app.MapControllers();
app.MapAdditionalIdentityEndpoints();

app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode()
   .AddInteractiveWebAssemblyRenderMode()
   .AddAdditionalAssemblies(typeof(BlazorApp3.Client._Imports).Assembly);

// Health check
app.MapGet("/api/test", () => "API works!");

// Migrate & seed
using (var scope = app.Services.CreateScope())
{
    var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var um = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
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
