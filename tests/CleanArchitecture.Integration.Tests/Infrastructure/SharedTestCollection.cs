namespace CleanArchitecture.Integration.Tests.Infrastructure;

[CollectionDefinition(Name)]
public class SharedTestCollection : ICollectionFixture<IntegrationTestWebAppFactory>
{
    public const string Name = "IntegrationApiTestCollection";
}