using LaPrimitiva.Domain.Entities;
using LaPrimitiva.Infrastructure.Persistence;
using LaPrimitiva.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace LaPrimitiva.Tests;

public class WinningDrawRepositoryTests
{
    [Fact]
    public async Task CreateAsync_WhenDrawIsInvalid_RejectsBeforePersistence()
    {
        var factory = CreateFactory();
        var repository = new WinningDrawRepository(factory);
        var draw = CreateValidDraw();
        draw.Number6 = 50;

        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.CreateAsync(draw));

        await using var context = await factory.CreateDbContextAsync();
        Assert.Empty(context.WinningDraws);
    }

    [Fact]
    public async Task UpdateAsync_WhenDrawIsInvalid_RejectsBeforePersistence()
    {
        var factory = CreateFactory();
        var repository = new WinningDrawRepository(factory);
        await using var context = await factory.CreateDbContextAsync();
        var draw = CreateValidDraw();
        context.WinningDraws.Add(draw);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var invalidUpdate = CreateValidDraw();
        invalidUpdate.Id = draw.Id;
        invalidUpdate.Reintegro = 10;

        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.UpdateAsync(invalidUpdate));

        Assert.Equal(0, (await context.WinningDraws.AsNoTracking().SingleAsync()).Reintegro);
    }

    private static IDbContextFactory<PrimitivaDbContext> CreateFactory()
    {
        var options = new DbContextOptionsBuilder<PrimitivaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestDbContextFactory(options);
    }

    private sealed class TestDbContextFactory(DbContextOptions<PrimitivaDbContext> options)
        : IDbContextFactory<PrimitivaDbContext>
    {
        public PrimitivaDbContext CreateDbContext() => new(options);

        public Task<PrimitivaDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }

    private static WinningDraw CreateValidDraw() => new()
    {
        DrawDate = new DateTime(2026, 8, 24),
        Number1 = 1,
        Number2 = 8,
        Number3 = 15,
        Number4 = 22,
        Number5 = 35,
        Number6 = 49,
        Complementario = 7,
        Reintegro = 0,
        Joker = "0123456"
    };
}
