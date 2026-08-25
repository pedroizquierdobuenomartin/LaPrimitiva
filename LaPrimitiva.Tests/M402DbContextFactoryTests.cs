using System.Collections.Concurrent;
using LaPrimitiva.Domain.Entities;
using LaPrimitiva.Infrastructure.Persistence;
using LaPrimitiva.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace LaPrimitiva.Tests;

public class M402DbContextFactoryTests
{
    [Fact]
    public async Task SimultaneousRepositoryOperations_UseDifferentDisposedContexts()
    {
        var factory = CreateFactory();
        var repository = new DrawRepository(factory);

        await Task.WhenAll(
            repository.AnyAsync(_ => true),
            repository.AnyAsync(_ => true));

        Assert.Equal(2, factory.CreatedContextIds.Count);
        Assert.Equal(2, factory.CreatedContextIds.Distinct().Count());
        Assert.Equal(2, factory.Disposals.Count);
    }

    [Fact]
    public async Task ReadOperation_ReturnsDetachedEntities_AndDisposesItsContext()
    {
        var factory = CreateFactory();
        await using (var arrangeContext = await factory.CreateDbContextAsync())
        {
            arrangeContext.WinningDraws.Add(CreateValidDraw());
            await arrangeContext.SaveChangesAsync();
        }

        factory.ResetObservations();
        var repository = new WinningDrawRepository(factory);

        var draws = await repository.GetListAsync();

        Assert.Single(draws);
        var disposal = Assert.Single(factory.Disposals);
        Assert.Equal(0, disposal.TrackedEntities);
    }

    private static RecordingDbContextFactory CreateFactory()
    {
        var options = new DbContextOptionsBuilder<PrimitivaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new RecordingDbContextFactory(options);
    }

    private static WinningDraw CreateValidDraw() => new()
    {
        DrawDate = new DateTime(2026, 8, 25),
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

    private sealed class RecordingDbContextFactory(DbContextOptions<PrimitivaDbContext> options)
        : IDbContextFactory<PrimitivaDbContext>
    {
        private readonly ConcurrentQueue<Guid> _createdContextIds = new();
        private readonly ConcurrentQueue<DisposalObservation> _disposals = new();

        public IReadOnlyCollection<Guid> CreatedContextIds => _createdContextIds.ToArray();
        public IReadOnlyCollection<DisposalObservation> Disposals => _disposals.ToArray();

        public PrimitivaDbContext CreateDbContext()
        {
            var contextId = Guid.NewGuid();
            _createdContextIds.Enqueue(contextId);
            return new ObservedPrimitivaDbContext(options, contextId, observation => _disposals.Enqueue(observation));
        }

        public Task<PrimitivaDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());

        public void ResetObservations()
        {
            _createdContextIds.Clear();
            _disposals.Clear();
        }
    }

    private sealed class ObservedPrimitivaDbContext(
        DbContextOptions<PrimitivaDbContext> options,
        Guid contextId,
        Action<DisposalObservation> observeDisposal) : PrimitivaDbContext(options)
    {
        private int _disposed;

        public override async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                observeDisposal(new DisposalObservation(contextId, ChangeTracker.Entries().Count()));
            }

            await base.DisposeAsync();
        }
    }

    private sealed record DisposalObservation(Guid ContextId, int TrackedEntities);
}
