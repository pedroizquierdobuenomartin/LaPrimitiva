using System.Globalization;
using System.Collections;
using LaPrimitiva.App.Exporting;
using LaPrimitiva.App.Localization;
using LaPrimitiva.Application.Services;
using LaPrimitiva.Domain.Entities;
using LaPrimitiva.Domain.Localization;
using LaPrimitiva.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace LaPrimitiva.Tests;

public sealed class M507LocalizationTests
{
    [Fact]
    public void ConfiguresEsEsForFormattingAndUi()
    {
        var options = LocalizationConfiguration.CreateRequestLocalizationOptions();

        Assert.Equal("es-ES", options.DefaultRequestCulture.Culture.Name);
        Assert.Equal("es-ES", options.DefaultRequestCulture.UICulture.Name);
        Assert.Equal(["es-ES"], options.SupportedCultures!.Select(culture => culture.Name));
        Assert.Equal(["es-ES"], options.SupportedUICultures!.Select(culture => culture.Name));
    }

    [Fact]
    public void ResolvesSpanishResourceAndParameters()
    {
        var resourceManager = new System.Resources.ResourceManager(typeof(GlobalResource));
        var errorResourceManager = new System.Resources.ResourceManager(typeof(ErrorResource));
        var culture = CultureInfo.GetCultureInfo("es-ES");

        Assert.Equal("Reintentar", resourceManager.GetString("Retry", culture));
        Assert.Equal(
            "Referencia: ABC123.",
            string.Format(culture, errorResourceManager.GetString("ErrorWithReference", culture)!, "ABC123"));
    }

    [Fact]
    public void ResolvesEverySpanishCatalogWithoutEmptyValues()
    {
        var culture = CultureInfo.GetCultureInfo("es-ES");
        Type[] resourceMarkers =
        [
            typeof(GlobalResource),
            typeof(LayoutResource),
            typeof(ReconnectionResource),
            typeof(ErrorResource),
            typeof(DashboardResource),
            typeof(RegistrationResource),
            typeof(PlansResource),
            typeof(HistoricalResource),
            typeof(CombinationResource),
            typeof(DataResource),
            typeof(HelpResource),
            typeof(PrivacyResource),
            typeof(TermsAndConditionsResource)
        ];

        foreach (var marker in resourceMarkers)
        {
            var resourceSet = new System.Resources.ResourceManager(marker)
                .GetResourceSet(culture, createIfNotExists: true, tryParents: false);

            Assert.NotNull(resourceSet);
            foreach (DictionaryEntry entry in resourceSet!)
            {
                Assert.False(string.IsNullOrWhiteSpace(entry.Value?.ToString()), $"{marker.Name}.{entry.Key}");
            }
        }
    }

    [Fact]
    public void ThrowsForMissingResourceKey()
    {
        var localizer = new RequiredStringLocalizer(new StubStringLocalizer(resourceNotFound: true));

        Assert.Throws<MissingLocalizationResourceException>(() => localizer["MissingKey"]);
    }

    [Fact]
    public void RejectsUnsupportedFutureCulture()
    {
        var options = LocalizationConfiguration.CreateRequestLocalizationOptions();

        Assert.DoesNotContain(options.SupportedCultures!, culture => culture.Name == "en-US");
        Assert.DoesNotContain(options.SupportedUICultures!, culture => culture.Name == "en-US");
    }

    [Theory]
    [InlineData("0,01", "0.01")]
    [InlineData("1,50", "1.50")]
    [InlineData("1.234,56", "1234.56")]
    [InlineData("-1.234,56", "-1234.56")]
    public void PreservesTypedValuesAcrossSpanishFormattingRoundTrip(string localized, string invariant)
    {
        var culture = CultureInfo.GetCultureInfo("es-ES");
        var value = decimal.Parse(localized, NumberStyles.Number, culture);

        Assert.Equal(decimal.Parse(invariant, CultureInfo.InvariantCulture), value);
        Assert.Equal(localized, value.ToString("N2", culture));
    }

    [Theory]
    [InlineData("01/02/2026", 2026, 2, 1)]
    [InlineData("29/02/2024", 2024, 2, 29)]
    [InlineData("31/01/2026", 2026, 1, 31)]
    [InlineData("01/03/2026", 2026, 3, 1)]
    public void PreservesAmbiguousAndBoundaryDates(string localized, int year, int month, int day)
    {
        var culture = CultureInfo.GetCultureInfo("es-ES");
        var parsed = DateOnly.ParseExact(localized, "dd/MM/yyyy", culture);

        Assert.Equal(new DateOnly(year, month, day), parsed);
        Assert.Equal(localized, parsed.ToString("dd/MM/yyyy", culture));
    }

    [Fact]
    public void PreservesBusinessDateWithoutTimeZoneShift()
    {
        var culture = CultureInfo.GetCultureInfo("es-ES");
        var businessDate = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var rendered = businessDate.ToString("d", culture);
        var parsed = DateTime.ParseExact(rendered, culture.DateTimeFormat.ShortDatePattern, culture);

        Assert.Equal(DateTimeKind.Unspecified, parsed.Kind);
        Assert.Equal(businessDate.Date, parsed.Date);
    }

    [Fact]
    public void KeepsCsvContractInvariantUnderSpanishUiCulture()
    {
        using var _ = new CultureScope("es-ES");
        var draw = new DrawRecord
        {
            WeekNumber = 1,
            DrawType = DrawType.Lunes,
            DrawDate = new DateTime(2026, 2, 1),
            CosteFija = 1.5m
        };

        var csv = CsvExportBuilder.Build([draw]);

        Assert.Contains("1,Lunes,2026-02-01,0,1.5,", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("1,5", csv, StringComparison.Ordinal);
    }

    [Fact]
    public async Task KeepsRssContractInvariantUnderSpanishUiCulture()
    {
        using var _ = new CultureScope("es-ES");
        const string xml = """
            <rss version="2.0"><channel><item>
              <description><![CDATA[<p><b>04 - 05 - 13 - 29 - 30 - 36</b> Complementario: <b>C(09)</b> Reintegro: <b>R(4)</b></p>]]></description>
              <pubDate>Sun, 01 Feb 2026 22:16:16 +0100</pubDate>
            </item></channel></rss>
            """;

        var draw = Assert.Single(await new RssParserService().ParseRssAsync(
            xml,
            TestContext.Current.CancellationToken));

        Assert.Equal(new DateTime(2026, 2, 1, 22, 16, 16), draw.Date);
    }

    [Fact]
    public void KeepsPersistenceValuesTypedAndPrecise()
    {
        var options = new DbContextOptionsBuilder<PrimitivaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var context = new PrimitivaDbContext(options);
        var draw = context.Model.FindEntityType(typeof(DrawRecord))!;

        Assert.Equal(10, draw.FindProperty(nameof(DrawRecord.CosteFija))!.GetPrecision());
        Assert.Equal(2, draw.FindProperty(nameof(DrawRecord.CosteFija))!.GetScale());
        Assert.Equal(typeof(decimal), draw.FindProperty(nameof(DrawRecord.CosteFija))!.ClrType);
        Assert.Equal(typeof(DateTime), draw.FindProperty(nameof(DrawRecord.DrawDate))!.ClrType);
    }

    private sealed class StubStringLocalizer(bool resourceNotFound) : IStringLocalizer
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _originalCulture = CultureInfo.CurrentCulture;
        private readonly CultureInfo _originalUiCulture = CultureInfo.CurrentUICulture;

        public CultureScope(string cultureName)
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(cultureName);
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _originalCulture;
            CultureInfo.CurrentUICulture = _originalUiCulture;
        }
    }
}
