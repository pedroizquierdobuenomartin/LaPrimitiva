using System.Threading;
using LaPrimitiva.Application.Interfaces;
using LaPrimitiva.Application.Services;
using LaPrimitiva.Domain.Interfaces;
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
                .Setup(service => service.GetLatestDrawDateAsync())
                .ReturnsAsync((DateTime?)null);
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
    }
}
