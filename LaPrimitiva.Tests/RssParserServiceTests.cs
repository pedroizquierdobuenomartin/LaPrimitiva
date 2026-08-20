using System;
using System.Linq;
using LaPrimitiva.Application.Services;
using LaPrimitiva.Domain.Models;
using Xunit;

namespace LaPrimitiva.Tests
{
    public class RssParserServiceTests
    {
        private readonly RssParserService _service;

        public RssParserServiceTests()
        {
            _service = new RssParserService();
        }

        [Fact]
        public void ParseRss_WithValidXml_ReturnsCorrectDraws()
        {
            // Arrange
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

            // Act
            var results = _service.ParseRss(xmlContent).ToList();

            // Assert
            Assert.Single(results);
            var draw = results[0];
            Assert.Equal(new DateTime(2026, 2, 2, 22, 16, 16), draw.Date);
            Assert.Equal(new int[] { 4, 5, 13, 29, 30, 36 }, draw.Numbers);
            Assert.Equal(9, draw.Complementary);
            Assert.Equal(4, draw.Reintegro);
            Assert.Equal(2114163, draw.Joker);
        }

        [Fact]
        public void ParseRss_WithEmptyXml_ReturnsEmptyList()
        {
            // Arrange
            string xmlContent = @"<rss version=""2.0""><channel></channel></rss>";

            // Act
            var results = _service.ParseRss(xmlContent);

            // Assert
            Assert.Empty(results);
        }

        [Theory]
        [InlineData("04-05-13-29-30-36")]
        [InlineData("04 -05- 13  -  29 - 30-36")]
        public void ParseRss_WithAllowedSeparatorSpacing_ReturnsCorrectNumbers(string numbers)
        {
            var xmlContent = BuildXml(
                $"<b>{numbers}</b> Complementario: <b>C(09)</b> Reintegro: <b>R(4)</b>");

            var draw = Assert.Single(_service.ParseRss(xmlContent));

            Assert.Equal(new[] { 4, 5, 13, 29, 30, 36 }, draw.Numbers);
        }

        [Fact]
        public void ParseRss_WithIncompleteItem_SkipsItem()
        {
            var xmlContent = BuildXml(
                "<b>04 - 05 - 13 - 29 - 30 - 36</b> Complementario: <b>C(09)</b>");

            var results = _service.ParseRss(xmlContent).ToList();

            Assert.Empty(results);
        }

        [Fact]
        public void ParseRss_WithMalformedDraw_SkipsItemWithoutThrowingDuringMaterialization()
        {
            var xmlContent = BuildXml(
                "<b>04-XX-13-29-30-36</b> Complementario: <b>C(09)</b> Reintegro: <b>R(4)</b>");

            List<RssDraw>? results = null;
            var exception = Record.Exception(() => results = _service.ParseRss(xmlContent).ToList());

            Assert.Null(exception);
            Assert.Empty(results!);
        }

        [Fact]
        public void ParseRss_WithMalformedXml_ReturnsEmptyList()
        {
            var results = _service.ParseRss("<rss><channel><item>").ToList();

            Assert.Empty(results);
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
