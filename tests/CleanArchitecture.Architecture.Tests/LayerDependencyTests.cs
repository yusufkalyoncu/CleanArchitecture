using FluentAssertions;
using NetArchTest.Rules;

namespace CleanArchitecture.Architecture.Tests;

public class LayerDependencyTests
{
    private const string ApplicationNamespace = "CleanArchitecture.Application";
    private const string InfrastructureNamespace = "CleanArchitecture.Infrastructure";
    private const string WebApiNamespace = "CleanArchitecture.WebApi";

    [Fact]
    public void Domain_Should_NotDependOnOtherLayers()
    {
        // Arrange
        var types = Types.InAssembly(Assemblies.Domain);
        types.GetTypes().Should().NotBeEmpty("Domain assembly should contain types.");

        // Act
        var result = types
            .ShouldNot()
            .HaveDependencyOnAll(
                ApplicationNamespace,
                InfrastructureNamespace,
                WebApiNamespace)
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Application_Should_NotDependOnInfrastructureOrWebApi()
    {
        // Arrange
        var types = Types.InAssembly(Assemblies.Application);
        types.GetTypes().Should().NotBeEmpty("Application assembly should contain types.");

        // Act
        var result = types
            .ShouldNot()
            .HaveDependencyOnAll(
                InfrastructureNamespace,
                WebApiNamespace)
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Infrastructure_Should_NotDependOnWebApi()
    {
        // Arrange
        var types = Types.InAssembly(Assemblies.Infrastructure);
        types.GetTypes().Should().NotBeEmpty("Infrastructure assembly should contain types.");

        // Act
        var result = types
            .ShouldNot()
            .HaveDependencyOn(WebApiNamespace)
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void WebApi_Should_NotDependOnInfrastructure_ExceptDependencyInjection()
    {
        // Arrange
        var types = Types.InAssembly(Assemblies.WebApi).That().ResideInNamespace("CleanArchitecture.WebApi.Endpoints");
        types.GetTypes().Should().NotBeEmpty("WebApi Endpoints should exist to test isolation.");

        // Act
        var result = types
            .ShouldNot()
            .HaveDependencyOn(InfrastructureNamespace)
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Domain_Should_NotDependOnEntityFramework()
    {
        // Arrange
        var types = Types.InAssembly(Assemblies.Domain);

        // Act
        var result = types
            .ShouldNot()
            .HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Application_Should_NotDependOnAspNet()
    {
        // Arrange
        var types = Types.InAssembly(Assemblies.Application);

        // Act
        var result = types
            .ShouldNot()
            .HaveDependencyOn("Microsoft.AspNetCore")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue();
    }
}