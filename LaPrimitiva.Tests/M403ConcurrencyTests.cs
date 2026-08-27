using LaPrimitiva.Domain.Entities;
using LaPrimitiva.Domain.Exceptions;
using LaPrimitiva.Infrastructure.Persistence;
using LaPrimitiva.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace LaPrimitiva.Tests;

public class M403ConcurrencyTests
{
    [Theory]
    [InlineData(typeof(Plan))]
    [InlineData(typeof(DrawRecord))]
    [InlineData(typeof(WinningDraw))]
    public void EditableEntities_ConfigureRowVersionAsGeneratedConcurrencyToken(Type entityType)
    {
        using var context = CreateContext();

        var property = context.Model.FindEntityType(entityType)!.FindProperty("RowVersion");

        Assert.NotNull(property);
        Assert.True(property!.IsConcurrencyToken);
        Assert.Equal(ValueGenerated.OnAddOrUpdate, property.ValueGenerated);
    }

    [Fact]
    public async Task DrawRepository_WhenDatabaseRejectsStaleToken_ReportsConcurrencyConflict()
    {
        var options = new DbContextOptionsBuilder<PrimitivaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var id = Guid.NewGuid();
        var plan = new Plan { Id = Guid.NewGuid(), Name = "Plan", EffectiveFrom = DateTime.Today };

        await using (var arrange = new PrimitivaDbContext(options))
        {
            arrange.Add(new DrawRecord
            {
                Id = id,
                PlanId = plan.Id,
                Plan = plan,
                DrawDate = DateTime.Today,
                RowVersion = [1]
            });
            await arrange.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var repository = new DrawRepository(new ThrowingFactory(options));
        var staleCopy = new DrawRecord
        {
            Id = id,
            PlanId = plan.Id,
            Plan = plan,
            DrawDate = DateTime.Today,
            Notes = "cambio obsoleto",
            RowVersion = [1]
        };

        var exception = await Assert.ThrowsAsync<ConcurrencyConflictException>(
            () => repository.UpdateAsync(staleCopy));

        Assert.Equal(id, exception.EntityId);
    }

    private static PrimitivaDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PrimitivaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PrimitivaDbContext(options);
    }

    private sealed class ThrowingFactory(DbContextOptions<PrimitivaDbContext> options)
        : IDbContextFactory<PrimitivaDbContext>
    {
        public PrimitivaDbContext CreateDbContext() => new ThrowingContext(options);

        public Task<PrimitivaDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }

    private sealed class ThrowingContext(DbContextOptions<PrimitivaDbContext> options)
        : PrimitivaDbContext(options)
    {
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new DbUpdateConcurrencyException("Conflicto simulado por M-403.");
    }
}
