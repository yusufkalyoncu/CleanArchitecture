using CleanArchitecture.Application.Abstractions.Messaging;
using FluentAssertions;
using NetArchTest.Rules;

namespace CleanArchitecture.Architecture.Tests;

public class NamingAndConventionTests
{
    [Fact]
    public void CommandHandlers_Should_EndWithCommandHandler()
    {
        var handlers = Types.InAssembly(Assemblies.Application)
            .That()
            .DoNotResideInNamespace("CleanArchitecture.Application.Abstractions.Behaviors")
            .And()
            .ImplementInterface(typeof(ICommandHandler<>))
            .Or()
            .ImplementInterface(typeof(ICommandHandler<,>))
            .And()
            .DoNotResideInNamespace("CleanArchitecture.Application.Abstractions.Behaviors");

        handlers.GetTypes().Should().NotBeEmpty();

        var result = handlers
            .Should()
            .HaveNameEndingWith("CommandHandler")
            .GetResult();

        result.IsSuccessful.Should().BeTrue($"because all command handlers should end with CommandHandler, but found {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void QueryHandlers_Should_EndWithQueryHandler()
    {
        var handlers = Types.InAssembly(Assemblies.Application)
            .That()
            .DoNotResideInNamespace("CleanArchitecture.Application.Abstractions.Behaviors")
            .And()
            .ImplementInterface(typeof(IQueryHandler<,>));

        handlers.GetTypes().Should().NotBeEmpty();

        var result = handlers
            .Should()
            .HaveNameEndingWith("QueryHandler")
            .GetResult();

        result.IsSuccessful.Should().BeTrue($"because all query handlers should end with QueryHandler, but found {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Commands_Should_EndWithCommand()
    {
        var commands = Types.InAssembly(Assemblies.Application)
            .That()
            .ImplementInterface(typeof(ICommand))
            .Or()
            .ImplementInterface(typeof(ICommand<>));

        commands.GetTypes().Should().NotBeEmpty();

        var result = commands
            .Should()
            .HaveNameEndingWith("Command")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Queries_Should_EndWithQuery()
    {
        var queries = Types.InAssembly(Assemblies.Application)
            .That()
            .ImplementInterface(typeof(IQuery<>));

        queries.GetTypes().Should().NotBeEmpty();

        var result = queries
            .Should()
            .HaveNameEndingWith("Query")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Validators_Should_EndWithValidator()
    {
        var validators = Types.InAssembly(Assemblies.Application)
            .That()
            .Inherit(typeof(FluentValidation.AbstractValidator<>));

        validators.GetTypes().Should().NotBeEmpty();

        var result = validators
            .Should()
            .HaveNameEndingWith("Validator")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Endpoints_Should_NotDependOnDbContext()
    {
        var endpoints = Types.InAssembly(Assemblies.WebApi)
            .That()
            .ResideInNamespace("CleanArchitecture.WebApi.Endpoints");

        endpoints.GetTypes().Should().NotBeEmpty();

        var result = endpoints
            .ShouldNot()
            .HaveDependencyOn("Microsoft.EntityFrameworkCore.DbContext")
            .And()
            .NotHaveDependencyOn("CleanArchitecture.Application.Abstractions.Database.IApplicationDbContext")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }
}