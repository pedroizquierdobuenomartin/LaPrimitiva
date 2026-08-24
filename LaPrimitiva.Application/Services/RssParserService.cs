using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using LaPrimitiva.Domain.Interfaces;
using LaPrimitiva.Domain.Models;

namespace LaPrimitiva.Application.Services
{
    public class RssParserService : IRssParserService
    {
        public async Task<IReadOnlyList<RssDraw>> ParseRssAsync(
            string xmlContent,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(xmlContent))
                return Array.Empty<RssDraw>();

            try
            {
                using var textReader = new StringReader(xmlContent);
                using var reader = XmlReader.Create(textReader, new XmlReaderSettings
                {
                    Async = true,
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                    MaxCharactersInDocument = RssFeedLimits.MaxBytes
                });

                var draws = new List<RssDraw>();
                var itemCount = 0;

                while (await reader.ReadAsync())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "item")
                        continue;

                    if (itemCount >= RssFeedLimits.MaxItems)
                        break;

                    itemCount++;
                    using var subtree = reader.ReadSubtree();
                    var item = await XElement.LoadAsync(subtree, LoadOptions.None, cancellationToken);
                    var draw = ParseItem(item);
                    if (draw is not null)
                        draws.Add(draw);
                }

                return draws;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return Array.Empty<RssDraw>();
            }
        }

        private RssDraw? ParseItem(XElement item)
        {
            var pubDateStr = item.Elements().FirstOrDefault(e => e.Name.LocalName == "pubDate")?.Value;
            var description = item.Elements().FirstOrDefault(e => e.Name.LocalName == "description")?.Value;

            if (string.IsNullOrEmpty(pubDateStr) || string.IsNullOrEmpty(description))
                return null;

            if (!DateTime.TryParse(pubDateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                // Fallback for some RSS formats if needed
                return null;
            }

            // Regex patterns for extraction - more flexible with whitespace and casing
            var numbersMatch = Regex.Match(description, @"<b>(\d{2}\s*-\s*\d{2}\s*-\s*\d{2}\s*-\s*\d{2}\s*-\s*\d{2}\s*-\s*\d{2})</b>", RegexOptions.IgnoreCase);
            var complementaryMatch = Regex.Match(description, @"Complementario:\s*<b>C\((\d{2})\)</b>", RegexOptions.IgnoreCase);
            var reintegroMatch = Regex.Match(description, @"Reintegro:\s*<b>R\((\d)\)</b>", RegexOptions.IgnoreCase);
            var jokerMatch = Regex.Match(description, @"Joker:\s*<b>J\((\d{7})\)</b>", RegexOptions.IgnoreCase);

            if (!numbersMatch.Success || !complementaryMatch.Success || !reintegroMatch.Success)
                return null;

            var numbers = numbersMatch.Groups[1].Value
                .Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(n => int.Parse(n))
                .ToArray();

            var complementary = int.Parse(complementaryMatch.Groups[1].Value);
            var reintegro = int.Parse(reintegroMatch.Groups[1].Value);
            int? joker = jokerMatch.Success ? int.Parse(jokerMatch.Groups[1].Value) : null;

            return new RssDraw(date, numbers, complementary, reintegro, joker);
        }
    }
}
