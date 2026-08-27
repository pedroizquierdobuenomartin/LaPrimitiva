using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LaPrimitiva.Application.Services;
using LaPrimitiva.Domain.Models;
using Xunit;

namespace LaPrimitiva.Tests
{
    public class RssParserServiceTests
    {
        private readonly RssParserService _service = new();

        [Fact]
        public async Task ParseRss_WithValidXml_ReturnsCorrectDraws()
        {
            string xmlContent = @"
<rss version=""2.0"">
    <channel>
        <item>
            <title>La Primitiva: resultados del lunes 02 de febrero de 2026</title>
            <link>https://www.loteriasyapuestas.es/es/la-primitiva/resultados/2026/02/02</link>
            <description><![CDATA[<p>Sorteo de La Primitiva del lunes 02 de febrero de 2026. Combinaci&oacute;n ganadora: <b>04 - 05 - 13 - 29 - 30 - 36</b> Complementario: <b>C(09)</b> Reintegro: <b>R(4)</b> Joker: <b>J(2114163)</b></p>]]></description>
            <pubDate>Mon, 02 Feb 2026 22:16:16 +0100</pubDate>
        </item>
    </channel>
</rss>";

            var results = await _service.ParseRssAsync(xmlContent, TestContext.Current.CancellationToken);

            var draw = Assert.Single(results);
            Assert.Equal(new DateTime(2026, 2, 2, 22, 16, 16), draw.Date);
            Assert.Equal(new[] { 4, 5, 13, 29, 30, 36 }, draw.Numbers);
            Assert.Equal(9, draw.Complementary);
            Assert.Equal(4, draw.Reintegro);
            Assert.Equal(2114163, draw.Joker);
        }

        [Fact]
        public async Task ParseRss_WithEmptyXml_ReturnsEmptyList()
        {
            var results = await _service.ParseRssAsync(
                @"<rss version=""2.0""><channel></channel></rss>",
                TestContext.Current.CancellationToken);

            Assert.Empty(results);
        }

        [Theory]
        [InlineData("04-05-13-29-30-36")]
        [InlineData("04 -05- 13  -  29 - 30-36")]
        public async Task ParseRss_WithAllowedSeparatorSpacing_ReturnsCorrectNumbers(string numbers)
        {
            var xmlContent = BuildXml(
                $"<b>{numbers}</b> Complementario: <b>C(09)</b> Reintegro: <b>R(4)</b>");

            var draw = Assert.Single(await _service.ParseRssAsync(xmlContent, TestContext.Current.CancellationToken));

            Assert.Equal(new[] { 4, 5, 13, 29, 30, 36 }, draw.Numbers);
        }

        [Fact]
        public async Task ParseRss_WithIncompleteItem_SkipsItem()
        {
            var xmlContent = BuildXml(
                "<b>04 - 05 - 13 - 29 - 30 - 36</b> Complementario: <b>C(09)</b>");

            var results = await _service.ParseRssAsync(xmlContent, TestContext.Current.CancellationToken);

            Assert.Empty(results);
        }

        [Fact]
        public async Task ParseRss_WithMalformedDraw_SkipsItemWithoutThrowingDuringMaterialization()
        {
            var xmlContent = BuildXml(
                "<b>04-XX-13-29-30-36</b> Complementario: <b>C(09)</b> Reintegro: <b>R(4)</b>");

            IReadOnlyList<RssDraw>? results = null;
            var exception = await Record.ExceptionAsync(
                async () => results = await _service.ParseRssAsync(xmlContent, TestContext.Current.CancellationToken));

            Assert.Null(exception);
            Assert.Empty(results!);
        }

        [Fact]
        public async Task ParseRss_WithMalformedXml_ReturnsEmptyList()
        {
            var results = await _service.ParseRssAsync(
                "<rss><channel><item>",
                TestContext.Current.CancellationToken);

            Assert.Empty(results);
        }

        [Fact]
        public async Task ParseRss_WithTooManyItems_StopsAtConfiguredLimit()
        {
            var item = """
                <item>
                    <description><![CDATA[<b>04-05-13-29-30-36</b> Complementario: <b>C(09)</b> Reintegro: <b>R(4)</b>]]></description>
                    <pubDate>Mon, 02 Feb 2026 22:16:16 +0100</pubDate>
                </item>
                """;
            var xmlContent =
                $"<rss><channel>{string.Concat(Enumerable.Repeat(item, RssFeedLimits.MaxItems + 1))}</channel></rss>";

            var results = await _service.ParseRssAsync(xmlContent, TestContext.Current.CancellationToken);

            Assert.Equal(RssFeedLimits.MaxItems, results.Count);
        }

        [Fact]
        public async Task ParseRss_WithCancellation_StopsParsing()
        {
            using var cancellationSource = new CancellationTokenSource();
            await cancellationSource.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                _service.ParseRssAsync(
                    BuildXml("<b>04-05-13-29-30-36</b> Complementario: <b>C(09)</b> Reintegro: <b>R(4)</b>"),
                    cancellationSource.Token));
        }

        private static string BuildXml(string description) => $"""
            <rss version="2.0">
                <channel>
                    <item>
                        <description><![CDATA[{description}]]></description>
                        <pubDate>Mon, 02 Feb 2026 22:16:16 +0100</pubDate>
                    </item>
                </channel>
            </rss>
            """;
    }
}
