using Xunit;

namespace LaPrimitiva.Tests.Integration;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class IntegrationTestCollection : ICollectionFixture<IntegrationTestFixture>
{
    public const string Name = "Integration tests";
}
