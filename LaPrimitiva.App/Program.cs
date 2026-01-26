using LaPrimitiva.Domain.Repositories;
using LaPrimitiva.Infrastructure.Repositories;
using LaPrimitiva.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using LaPrimitiva.Application.Services;
using LaPrimitiva.Application.Interfaces;
using LaPrimitiva.App.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddLocalization();

// Register DbContext
builder.Services.AddDbContext<PrimitivaDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register Repositories
builder.Services.AddScoped<IPlanRepository, PlanRepository>();
builder.Services.AddScoped<IDrawRepository, DrawRepository>();

// Register Application Services
builder.Services.AddScoped<PlanService>();
builder.Services.AddScoped<DrawGenerationService>();
builder.Services.AddScoped<SummaryService>();
builder.Services.AddScoped<GlobalState>();
builder.Services.AddScoped<IDrawService, DrawService>();

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
    var context = scope.ServiceProvider.GetRequiredService<PrimitivaDbContext>();
    var genService = scope.ServiceProvider.GetRequiredService<DrawGenerationService>();
    
    context.Database.EnsureCreated();

    if (!context.Plans.Any())
    {
        var plan = new LaPrimitiva.Domain.Entities.Plan
        {
            Name = "Plan 2026",
            EffectiveFrom = new DateTime(2026, 1, 1),
            EffectiveTo = new DateTime(2026, 12, 31),
            EnableJoker = true,
            CostPerBet = 1.00m,
            JokerCostPerBet = 1.00m,
            FixedCombinationLabel = "Combinación Fija Estándar"
        };
        context.Plans.Add(plan);
        await context.SaveChangesAsync();

        var draws = genService.GenerateDrawsForRange(plan.Id, plan.EffectiveFrom, plan.EffectiveTo.Value);
        context.DrawRecords.AddRange(draws);
        await context.SaveChangesAsync();
    }
}

app.Run();
namespace LaPrimitiva.App { public partial class Program { } }
