using LaPrimitiva.Domain.Repositories;
using LaPrimitiva.Infrastructure.Persistence.Seed;
using LaPrimitiva.App.Models;
using LaPrimitiva.Infrastructure.Repositories;
using LaPrimitiva.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using LaPrimitiva.Application.Services;
using LaPrimitiva.Application.Interfaces;
using LaPrimitiva.App.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Configuration.AddJsonFile("reconnection.json", optional: false, reloadOnChange: true);
builder.Services.Configure<ReconnectionLabels>(builder.Configuration.GetSection("ReconnectionLabels"));

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddLocalization();

// Register DbContext
builder.Services.AddDbContext<PrimitivaDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register Repositories
builder.Services.AddScoped<IPlanRepository, PlanRepository>();
builder.Services.AddScoped<IDrawRepository, DrawRepository>();
builder.Services.AddScoped<IWinningDrawRepository, WinningDrawRepository>();

// Register Application Services
builder.Services.AddScoped<PlanService>();
builder.Services.AddScoped<DrawGenerationService>();
builder.Services.AddScoped<SummaryService>();
builder.Services.AddScoped<GlobalState>();
builder.Services.AddScoped<IDrawService, DrawService>();
builder.Services.AddScoped<IWinningDrawService, WinningDrawService>();
builder.Services.AddScoped<WinningDrawSeeder>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Seed data
using (var scope = app.Services.CreateScope())
{
    // Base Table and Data Initialization (Robust Check)
    var winningSeeder = scope.ServiceProvider.GetRequiredService<WinningDrawSeeder>();
    var seedPath = Path.Combine(AppContext.BaseDirectory, "SeedData");
    
    // This creates Tables if missing and seeds historical results
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

app.Run();
namespace LaPrimitiva.App { public partial class Program { } }
