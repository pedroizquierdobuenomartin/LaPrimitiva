using LaPrimitiva.Domain.Repositories;
using LaPrimitiva.Infrastructure.Persistence.Seed;
using LaPrimitiva.Infrastructure.Repositories;
using LaPrimitiva.Infrastructure.Persistence;
using LaPrimitiva.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using LaPrimitiva.Application.Services;
using LaPrimitiva.Application.Interfaces;
using LaPrimitiva.App.Components;
using LaPrimitiva.Domain.Interfaces;
using LaPrimitiva.Domain.Models;
using LaPrimitiva.App.Security;
using LaPrimitiva.App.Observability;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json;
using LaPrimitiva.App.Localization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Localization;

var builder = WebApplication.CreateBuilder(args);

LocalOnlyPolicy.ValidateStartupConfiguration(builder.Configuration);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffK";
    options.JsonWriterOptions = new JsonWriterOptions { Indented = false };
});
builder.Logging.AddProvider(new SecureJsonFileLoggerProvider(
    Path.Combine(builder.Environment.ContentRootPath, "logs")));

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddLocalization();
builder.Services.AddSingleton<IStringLocalizerFactory, RequiredStringLocalizerFactory>();
var requestLocalizationOptions = LocalizationConfiguration.CreateRequestLocalizationOptions();
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = requestLocalizationOptions.DefaultRequestCulture;
    options.SupportedCultures = requestLocalizationOptions.SupportedCultures;
    options.SupportedUICultures = requestLocalizationOptions.SupportedUICultures;
    options.ApplyCurrentCultureToResponseHeaders = requestLocalizationOptions.ApplyCurrentCultureToResponseHeaders;
});

// Create a short-lived DbContext for each data operation. A scoped DbContext
// would otherwise live for the complete interactive Blazor circuit.
builder.Services.AddDbContextFactory<PrimitivaDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);

// Register Repositories
builder.Services.AddScoped<IPlanRepository, PlanRepository>();
builder.Services.AddScoped<IDrawRepository, DrawRepository>();
builder.Services.AddScoped<IWinningDrawRepository, WinningDrawRepository>();

// Register Application Services
builder.Services.AddScoped<PlanService>();
builder.Services.AddScoped<SummaryService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IDataExportService, DataExportService>();
builder.Services.AddScoped<GlobalState>();
builder.Services.AddScoped<IDrawService, DrawService>();
builder.Services.AddScoped<IWinningDrawService, WinningDrawService>();
builder.Services.AddScoped<WinningDrawSeeder>();

// Register Notification Services
builder.Services.AddHttpClient<IRssClient, RssClient>();
builder.Services.AddScoped<IRssParserService, RssParserService>();
builder.Services.AddScoped<ILocalStorageService, LocalStorageService>();
builder.Services.AddScoped<IDrawNotificationService, DrawNotificationService>();
builder.Services.AddScoped<IApplicationErrorReporter, ApplicationErrorReporter>();
builder.Services.AddScoped<IAutomatedCombinationService, AutomatedCombinationService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseMiddleware<LocalOnlyMiddleware>();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseHttpsRedirection();
app.UseRequestLocalization(requestLocalizationOptions);

app.UseAntiforgery();

app.MapStaticAssets();
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = WriteHealthResponseAsync
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
    ResponseWriter = WriteHealthResponseAsync
});
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Integration tests own their database lifecycle and seed only explicit test resources.
if (!app.Environment.IsEnvironment("IntegrationTests"))
{
    using (var scope = app.Services.CreateScope())
    {
        // Data initialization only. Schema changes are applied administratively
        // through scripts/Invoke-M401DatabaseMigration.ps1 before startup.
        var winningSeeder = scope.ServiceProvider.GetRequiredService<WinningDrawSeeder>();
        var seedPath = Path.Combine(AppContext.BaseDirectory, "SeedData");

        // This repairs derived totals and seeds historical results; it never runs DDL.
        await winningSeeder.SeedFromDirectoryAsync(seedPath);

        // Initial Plan Seed (Optional, currently disabled to ensure "sin datos" as requested)
        /*
        if (!context.Plans.Any())
        {
            var plan = new LaPrimitiva.Domain.Entities.Plan
            {
                Name = "Plan 2026",
                // ...
            }
        }
        */
    }
}

static Task WriteHealthResponseAsync(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json";
    context.Response.Headers.CacheControl = "no-store";

    // Deliberately expose only aggregate status; exception details stay in structured logs.
    return context.Response.WriteAsJsonAsync(new
    {
        status = report.Status.ToString(),
        correlationId = context.TraceIdentifier
    });
}

app.Run();
namespace LaPrimitiva.App { public partial class Program { } }
