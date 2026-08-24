using System.Net;
using LaPrimitiva.App.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace LaPrimitiva.Tests;

public class LocalOnlySecurityTests
{
    [Theory]
    [InlineData("http://localhost:5007")]
    [InlineData("https://127.0.0.1:7001")]
    [InlineData("http://[::1]:5007")]
    public void ValidateStartupConfiguration_ShouldAcceptLoopbackUrls(string url)
    {
        var configuration = CreateConfiguration(("urls", url));

        var exception = Record.Exception(
            () => LocalOnlyPolicy.ValidateStartupConfiguration(configuration));

        Assert.Null(exception);
    }

    [Theory]
    [InlineData("http://0.0.0.0:5007")]
    [InlineData("http://*:5007")]
    [InlineData("http://192.168.1.20:5007")]
    [InlineData("http://laprimitiva.local:5007")]
    public void ValidateStartupConfiguration_ShouldRejectNonLoopbackUrls(string url)
    {
        var configuration = CreateConfiguration(("urls", url));

        Assert.Throws<InvalidOperationException>(
            () => LocalOnlyPolicy.ValidateStartupConfiguration(configuration));
    }

    [Fact]
    public void ValidateStartupConfiguration_ShouldRejectWildcardPortConfiguration()
    {
        var configuration = CreateConfiguration(("http_ports", "5007"));

        Assert.Throws<InvalidOperationException>(
            () => LocalOnlyPolicy.ValidateStartupConfiguration(configuration));
    }

    [Fact]
    public void ValidateStartupConfiguration_ShouldRejectNonLoopbackKestrelEndpoint()
    {
        var configuration = CreateConfiguration(
            ("Kestrel:Endpoints:Http:Url", "http://+:5007"));

        Assert.Throws<InvalidOperationException>(
            () => LocalOnlyPolicy.ValidateStartupConfiguration(configuration));
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    [InlineData("::ffff:127.0.0.1")]
    public async Task LocalOnlyMiddleware_ShouldAllowLoopbackClients(string address)
    {
        var nextCalled = false;
        var middleware = new LocalOnlyMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            NullLogger<LocalOnlyMiddleware>.Instance);
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(address);

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }

    [Theory]
    [InlineData("192.168.1.10")]
    [InlineData("203.0.113.20")]
    public async Task LocalOnlyMiddleware_ShouldRejectNonLoopbackClients(string address)
    {
        var nextCalled = false;
        var middleware = new LocalOnlyMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            NullLogger<LocalOnlyMiddleware>.Instance);
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(address);

        await middleware.InvokeAsync(context);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    private static IConfiguration CreateConfiguration(params (string Key, string Value)[] values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(
                value => value.Key,
                value => (string?)value.Value))
            .Build();
    }
}
