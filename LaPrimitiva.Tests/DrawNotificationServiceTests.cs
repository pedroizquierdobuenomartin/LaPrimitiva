using System.Linq.Expressions;
using System.Threading;
using LaPrimitiva.Application.Interfaces;
using LaPrimitiva.Application.Services;
using LaPrimitiva.Domain.Entities;
using LaPrimitiva.Domain.Interfaces;
using LaPrimitiva.Domain.Models;
using LaPrimitiva.Domain.Repositories;
using Moq;

namespace LaPrimitiva.Tests
{
    public class DrawNotificationServiceTests
    {
        [Fact]
        public async Task CheckForNewDrawsAsync_WhenUpdateIsRunning_DoesNotStartAnotherDownload()
        {
            var rssClient = new BlockingRssClient();
            var winningDrawService = new Mock<IWinningDrawService>();
            winningDrawService
                .Setup(service => service.GetExistingDrawDatesAsync(It.IsAny<IReadOnlyCollection<DateTime>>()))
                .ReturnsAsync([]);
            var service = new DrawNotificationService(
                rssClient,
                new RssParserService(),
                winningDrawService.Object,
                new GlobalState(),
                Mock.Of<IApplicationErrorReporter>());

            var firstUpdate = service.CheckForNewDrawsAsync(TestContext.Current.CancellationToken);
            await rssClient.Started.Task.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

            var secondUpdate = service.CheckForNewDrawsAsync(TestContext.Current.CancellationToken);
            await secondUpdate.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

            Assert.Equal(1, rssClient.SendCount);

            rssClient.Release.TrySetResult();
            await firstUpdate.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        }

        [Fact]
        public async Task CheckForNewDrawsAsync_WhenNewestDrawExists_KeepsOlderHistoricalGapsPending()
        {
            var draws = CreateRssDraws();
            var repository = new InMemoryWinningDrawRepository();
            await repository.CreateAsync(ToEntity(draws[0]));
            var winningDrawService = new WinningDrawService(repository);
            var globalState = new GlobalState();
            var service = CreateNotificationService(draws, winningDrawService, globalState);

            await service.CheckForNewDrawsAsync(TestContext.Current.CancellationToken);

            Assert.Equal([draws[1].Date, draws[2].Date], globalState.RecentDraws.Select(draw => draw.Date));
        }

        [Fact]
        public async Task CheckForNewDrawsAsync_WhenDrawsAreSavedOutOfOrder_RemovesOnlyEachSavedDraw()
        {
            var draws = CreateRssDraws();
            var repository = new InMemoryWinningDrawRepository();
            var winningDrawService = new WinningDrawService(repository);
            var globalState = new GlobalState();
            var service = CreateNotificationService(draws, winningDrawService, globalState);

            await service.CheckForNewDrawsAsync(TestContext.Current.CancellationToken);
            Assert.Equal(draws.Select(draw => draw.Date), globalState.RecentDraws.Select(draw => draw.Date));

            Assert.True((await winningDrawService.SaveFromRssAsync(draws[0])).IsSuccess);
            await service.CheckForNewDrawsAsync(TestContext.Current.CancellationToken);
            Assert.Equal([draws[1].Date, draws[2].Date], globalState.RecentDraws.Select(draw => draw.Date));

            Assert.True((await winningDrawService.SaveFromRssAsync(draws[2])).IsSuccess);
            await service.CheckForNewDrawsAsync(TestContext.Current.CancellationToken);
            Assert.Equal([draws[1].Date], globalState.RecentDraws.Select(draw => draw.Date));

            Assert.False((await winningDrawService.SaveFromRssAsync(draws[0])).IsSuccess);
            await service.CheckForNewDrawsAsync(TestContext.Current.CancellationToken);
            Assert.Equal([draws[1].Date], globalState.RecentDraws.Select(draw => draw.Date));
        }

        [Fact]
        public async Task CheckForNewDrawsAsync_WhenMoreThanTenDrawsArePending_CountsAllAndBoundsPopupItems()
        {
            var draws = Enumerable.Range(0, 12)
                .Select(index => new RssDraw(
                    new DateTime(2026, 8, 31).AddDays(-index),
                    [1, 2, 3, 4, 5, 6],
                    7,
                    8,
                    1234567))
                .ToList();
            var globalState = new GlobalState();
            var service = CreateNotificationService(
                draws,
                new WinningDrawService(new InMemoryWinningDrawRepository()),
                globalState);

            await service.CheckForNewDrawsAsync(TestContext.Current.CancellationToken);

            Assert.Equal(12, globalState.NewDrawsCount);
            Assert.Equal(10, globalState.RecentDraws.Count);
        }

        private static DrawNotificationService CreateNotificationService(
            IReadOnlyList<RssDraw> draws,
            IWinningDrawService winningDrawService,
            GlobalState globalState)
        {
            var rssClient = new Mock<IRssClient>();
            rssClient
                .Setup(client => client.GetRssXmlAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync("<rss />");
            var rssParser = new Mock<IRssParserService>();
            rssParser
                .Setup(parser => parser.ParseRssAsync("<rss />", It.IsAny<CancellationToken>()))
                .ReturnsAsync(draws);

            return new DrawNotificationService(
                rssClient.Object,
                rssParser.Object,
                winningDrawService,
                globalState,
                Mock.Of<IApplicationErrorReporter>());
        }

        private static IReadOnlyList<RssDraw> CreateRssDraws() =>
        [
            new(new DateTime(2026, 8, 20, 22, 0, 0), [1, 2, 3, 4, 5, 6], 7, 8, 1234567),
            new(new DateTime(2026, 8, 17, 21, 30, 0), [8, 9, 10, 11, 12, 13], 14, 5, 2345678),
            new(new DateTime(2026, 8, 13, 22, 15, 0), [15, 16, 17, 18, 19, 20], 21, 2, 3456789)
        ];

        private static WinningDraw ToEntity(RssDraw draw) => new()
        {
            Id = Guid.NewGuid(),
            DrawDate = draw.Date.Date,
            Number1 = draw.Numbers[0],
            Number2 = draw.Numbers[1],
            Number3 = draw.Numbers[2],
            Number4 = draw.Numbers[3],
            Number5 = draw.Numbers[4],
            Number6 = draw.Numbers[5],
            Complementario = draw.Complementary,
            Reintegro = draw.Reintegro,
            Joker = draw.Joker?.ToString("D7")
        };

        private sealed class BlockingRssClient : IRssClient
        {
            public TaskCompletionSource Started { get; } =
                new(TaskCreationOptions.RunContinuationsAsynchronously);

            public TaskCompletionSource Release { get; } =
                new(TaskCreationOptions.RunContinuationsAsynchronously);

            public int SendCount { get; private set; }

            public async Task<string?> GetRssXmlAsync(CancellationToken cancellationToken = default)
            {
                SendCount++;
                Started.TrySetResult();
                await Release.Task.WaitAsync(cancellationToken);
                return """
                    <rss><channel><item>
                        <description><![CDATA[<b>04-05-13-29-30-36</b> Complementario: <b>C(09)</b> Reintegro: <b>R(4)</b>]]></description>
                        <pubDate>Mon, 02 Feb 2026 22:16:16 +0100</pubDate>
                    </item></channel></rss>
                    """;
            }
        }

        private sealed class InMemoryWinningDrawRepository : IWinningDrawRepository
        {
            private readonly List<WinningDraw> _draws = [];

            public Task<List<WinningDraw>> GetListAsync(Expression<Func<WinningDraw, bool>>? predicate = null)
            {
                var query = predicate is null ? _draws : _draws.Where(predicate.Compile());
                return Task.FromResult(query.OrderByDescending(draw => draw.DrawDate).ToList());
            }

            public Task<List<int>> GetYearsAsync() =>
                Task.FromResult(_draws.Select(draw => draw.DrawDate.Year).Distinct().ToList());

            public Task<WinningDraw?> GetAsync(Guid id) =>
                Task.FromResult(_draws.SingleOrDefault(draw => draw.Id == id));

            public Task<DateTime?> GetLatestDateAsync() =>
                Task.FromResult(_draws.Count == 0 ? (DateTime?)null : _draws.Max(draw => draw.DrawDate));

            public Task<bool> AnyAsync(Expression<Func<WinningDraw, bool>> predicate) =>
                Task.FromResult(_draws.Any(predicate.Compile()));

            public Task CreateAsync(WinningDraw draw)
            {
                _draws.Add(draw);
                return Task.CompletedTask;
            }

            public Task UpdateAsync(WinningDraw draw) => Task.CompletedTask;

            public Task DeleteAsync(Guid id)
            {
                _draws.RemoveAll(draw => draw.Id == id);
                return Task.CompletedTask;
            }
        }
    }
}
