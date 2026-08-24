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
        await using var context = CreateContext();
        var repository = new WinningDrawRepository(context);
        var draw = CreateValidDraw();
        draw.Number6 = 50;

        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.CreateAsync(draw));

        Assert.Empty(context.WinningDraws);
    }

    [Fact]
    public async Task UpdateAsync_WhenDrawIsInvalid_RejectsBeforePersistence()
    {
        await using var context = CreateContext();
        var repository = new WinningDrawRepository(context);
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

    private static PrimitivaDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PrimitivaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PrimitivaDbContext(options);
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
