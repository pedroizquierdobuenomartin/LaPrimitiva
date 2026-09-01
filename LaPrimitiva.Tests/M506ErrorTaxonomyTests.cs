using System.Net;
using System.Linq.Expressions;
using LaPrimitiva.Application.Services;
using LaPrimitiva.Domain.Errors;
using LaPrimitiva.Domain.Exceptions;
using LaPrimitiva.Domain.Entities;
using LaPrimitiva.Domain.Repositories;
using LaPrimitiva.Infrastructure.Persistence;
using LaPrimitiva.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace LaPrimitiva.Tests;

public class M506ErrorTaxonomyTests
{
    [Fact]
    public void Concurrency_RequiresReloadAndIsNotRetryableWithoutFreshData()
    {
        var exception = new ConcurrencyConflictException(Guid.NewGuid());

        Assert.Equal(ErrorCategory.Concurrency, exception.Error.Category);
        Assert.Equal(ErrorRecoveryAction.Reload, exception.Error.RecoveryAction);
        Assert.False(exception.Error.IsRetryable);
        Assert.Contains("EntityId", exception.Context.Keys);
    }

    [Fact]
    public void UniqueConstraint_IsTranslatedWithoutProviderMessage()
    {
        var provider = new InvalidOperationException("duplicate key dbo.WinningDraws IX_secret");

        var exception = Assert.IsType<DataIntegrityException>(
            PersistenceExceptionTranslator.TranslateSqlServerError(2627, "WinningDraw.Create", provider));

        Assert.Equal("persistence.unique-constraint", exception.IntegrityCode);
        Assert.DoesNotContain("dbo.", exception.SafeMessage);
        Assert.Same(provider, exception.InnerException);
    }

    [Fact]
    public async Task EntityNotFound_IsRaisedByTheApplicationUseCase()
    {
        var drawRepository = new Mock<IDrawRepository>();
        var planRepository = new Mock<IPlanRepository>();
        drawRepository
            .Setup(repository => repository.AnyAsync(It.IsAny<Expression<Func<DrawRecord, bool>>>() ))
            .ReturnsAsync(false);
        planRepository.Setup(repository => repository.GetAsync(It.IsAny<Guid>())).ReturnsAsync((Plan?)null);
        var service = new DrawService(drawRepository.Object, planRepository.Object);

        var exception = await Assert.ThrowsAsync<EntityNotFoundException>(
            () => service.ValidateDrawAsync(Guid.NewGuid(), new DateTime(2026, 8, 27)));

        Assert.Equal(ErrorCategory.NotFound, exception.Error.Category);
        Assert.Equal(ErrorRecoveryAction.GoBack, exception.Error.RecoveryAction);
        Assert.Contains("EntityId", exception.Context.Keys);
    }

    [Fact]
    public void DatabaseUnavailable_IsTranslatedAsRetryable()
    {
        var exception = Assert.IsType<PersistenceUnavailableException>(
            PersistenceExceptionTranslator.TranslateSqlServerError(53, "Plan.List"));

        Assert.Equal(ErrorCategory.PersistenceUnavailable, exception.Error.Category);
        Assert.True(exception.Error.IsRetryable);
        Assert.Equal(ErrorRecoveryAction.Retry, exception.Error.RecoveryAction);
    }

    [Fact]
    public async Task HttpTimeout_IsDifferentFromCallerCancellation()
    {
        using var httpClient = new HttpClient(new StubHandler(_ =>
            throw new TaskCanceledException("provider timeout")))
        {
            Timeout = TimeSpan.FromSeconds(2)
        };
        var client = new RssClient(httpClient, NullLogger<RssClient>.Instance);

        var exception = await Assert.ThrowsAsync<ExternalServiceTimeoutException>(
            () => client.GetRssXmlAsync(TestContext.Current.CancellationToken));

        Assert.Equal("external.timeout", exception.Error.Code);
        Assert.True(exception.Error.IsRetryable);
    }

    [Fact]
    public async Task HttpUnavailable_IsTranslatedWithoutLeakingTheProviderMessage()
    {
        using var httpClient = new HttpClient(new StubHandler(_ =>
            throw new HttpRequestException("DNS failed for internal-provider-host")));
        var client = new RssClient(httpClient, NullLogger<RssClient>.Instance);

        var exception = await Assert.ThrowsAsync<ExternalServiceUnavailableException>(
            () => client.GetRssXmlAsync(TestContext.Current.CancellationToken));

        Assert.Equal(ErrorCategory.ExternalUnavailable, exception.Error.Category);
        Assert.DoesNotContain("internal-provider-host", exception.SafeMessage);
    }

    [Fact]
    public async Task InvalidRss_IsTranslatedAsExternalInvalidData()
    {
        var parser = new RssParserService();

        var exception = await Assert.ThrowsAsync<ExternalDataFormatException>(
            () => parser.ParseRssAsync("<rss><channel><item>", TestContext.Current.CancellationToken));

        Assert.Equal(ErrorCategory.ExternalInvalidData, exception.Error.Category);
        Assert.DoesNotContain("XmlException", exception.SafeMessage);
    }

    [Fact]
    public void Unexpected_PresentationDoesNotExposeTechnicalDetails()
    {
        var error = ApplicationError.FromException(
            new Exception("Server=LOCALSERVER;Password=secret; SELECT * FROM Bets"));

        Assert.Equal(ErrorCategory.Unexpected, error.Category);
        Assert.Equal(ErrorCatalog.Unexpected.SafeMessage, error.Message);
        Assert.DoesNotContain("LOCALSERVER", error.Message);
        Assert.DoesNotContain("Password", error.Message);
    }

    [Fact]
    public void Unexpected_BlazorBoundaryLogsOnceAndShowsOnlyAReference()
    {
        var boundary = ReadRepoFile("LaPrimitiva.App/Components/Shared/AppErrorBoundary.razor");
        var reporter = ReadRepoFile("LaPrimitiva.App/Observability/ApplicationErrorReporter.cs");

        Assert.Contains("ErrorReporter.Report(exception, \"BlazorCircuit\")", boundary);
        Assert.Contains("LG[\"ReferenceLabel\"]", boundary);
        Assert.DoesNotContain("CurrentException.Message", boundary);
        Assert.Contains("ErrorReference", reporter);
        Assert.Contains("ErrorCategory", reporter);
        Assert.Contains("Operation", reporter);
    }

    private sealed class StubHandler(Func<CancellationToken, Task<HttpResponseMessage>> sendAsync)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => sendAsync(cancellationToken);
    }

    private static string ReadRepoFile(string relativePath) =>
        File.ReadAllText(Path.Combine(FindRepoRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "LaPrimitiva.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("No se encontró la raíz del repositorio.");
    }
}
