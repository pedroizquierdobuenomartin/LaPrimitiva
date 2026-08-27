using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using LaPrimitiva.Domain.Errors;
using LaPrimitiva.Domain.Models;
using LaPrimitiva.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace LaPrimitiva.Tests
{
    public class RssClientTests
    {
        [Fact]
        public async Task GetRssXmlAsync_WithOversizedContentLength_RejectsFeed()
        {
            using var httpClient = new HttpClient(new StubHandler(_ =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(new byte[RssFeedLimits.MaxBytes + 1])
                })));
            var client = CreateClient(httpClient);

            await Assert.ThrowsAsync<ExternalDataFormatException>(
                () => client.GetRssXmlAsync(TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task GetRssXmlAsync_WithOversizedChunkedBody_StopsStreamingAtByteLimit()
        {
            using var httpClient = new HttpClient(new StubHandler(_ =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StreamContent(new MemoryStream(new byte[RssFeedLimits.MaxBytes + 1]))
                })));
            var client = CreateClient(httpClient);

            await Assert.ThrowsAsync<ExternalDataFormatException>(
                () => client.GetRssXmlAsync(TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task GetRssXmlAsync_WithCancellation_StopsRequest()
        {
            using var httpClient = new HttpClient(new StubHandler(async cancellationToken =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK);
            }));
            var client = CreateClient(httpClient);
            using var cancellationSource = new CancellationTokenSource();
            cancellationSource.CancelAfter(TimeSpan.FromMilliseconds(50));

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                client.GetRssXmlAsync(cancellationSource.Token));
        }

        private sealed class StubHandler(
            Func<CancellationToken, Task<HttpResponseMessage>> sendAsync) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken) => sendAsync(cancellationToken);
        }

        private static RssClient CreateClient(HttpClient httpClient) =>
            new(httpClient, NullLogger<RssClient>.Instance);
    }
}
