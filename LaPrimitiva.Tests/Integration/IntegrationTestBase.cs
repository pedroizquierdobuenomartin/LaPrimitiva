using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LaPrimitiva.Tests.Integration
{
    public abstract class IntegrationTestBase : IAsyncLifetime
    {
        private readonly IntegrationTestFixture _fixture;
        protected readonly WebApplicationFactory<LaPrimitiva.App.Program> _factory;

        protected IntegrationTestBase(IntegrationTestFixture fixture)
        {
            _fixture = fixture;
            _factory = fixture.Factory;
        }

        public ValueTask InitializeAsync()
        {
            return new ValueTask(_fixture.ResetDatabaseAsync());
        }

        protected Task ResetDatabaseAsync()
        {
            return _fixture.ResetDatabaseAsync();
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }

        protected IServiceScope CreateScope() => _factory.Services.CreateScope();
    }
}
