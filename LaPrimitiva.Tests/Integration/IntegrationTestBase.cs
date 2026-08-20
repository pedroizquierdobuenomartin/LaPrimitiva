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

        public Task InitializeAsync()
        {
            return _fixture.ResetDatabaseAsync();
        }

        protected Task ResetDatabaseAsync()
        {
            return _fixture.ResetDatabaseAsync();
        }

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }

        protected IServiceScope CreateScope() => _factory.Services.CreateScope();
    }
}
