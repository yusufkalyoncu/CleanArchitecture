using CleanArchitecture.Application.Abstractions.Messaging;
using CleanArchitecture.Shared;
using FluentAssertions;
using NetArchTest.Rules;

namespace CleanArchitecture.Architecture.Tests;

public class DesignRulesTests
{
    [Fact]
    public void Entities_Should_NotHavePublicSetters()
    {
        // Entity or AggregateRoot classes shouldn't allow property mutation from outside (public setters).
        // Only private/internal setters or init-only properties should be used.
        var types = Types.InAssembly(Assemblies.Domain)
            .That()
            .Inherit(typeof(Entity)); // Entity is the base for both Entity and AggregateRoot

        types.GetTypes().Should().NotBeEmpty();

        var result = types
            .Should()
            .BeImmutable() // BeImmutable in NetArchTest actually checks if properties have public setters (or if they are init-only).
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Handlers_Should_BeSealed()
    {
        var commandHandlers = Types.InAssembly(Assemblies.Application)
            .That()
            .ImplementInterface(typeof(ICommandHandler<>))
            .Or()
            .ImplementInterface(typeof(ICommandHandler<,>));
            
        commandHandlers.GetTypes().Should().NotBeEmpty();

        var result = commandHandlers
            .Should()
            .BeSealed()
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }
    
    [Fact]
    public void Handlers_Should_BeInternal()
    {
        var commandHandlers = Types.InAssembly(Assemblies.Application)
            .That()
            .ImplementInterface(typeof(ICommandHandler<>))
            .Or()
            .ImplementInterface(typeof(ICommandHandler<,>));
            
        commandHandlers.GetTypes().Should().NotBeEmpty();

        var result = commandHandlers
            .Should()
            .NotBePublic()
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Validators_Should_BeSealed()
    {
        var validators = Types.InAssembly(Assemblies.Application)
            .That()
            .Inherit(typeof(FluentValidation.AbstractValidator<>));
            
        validators.GetTypes().Should().NotBeEmpty();

        var result = validators
            .Should()
            .BeSealed()
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void DomainEvents_Should_BeSealedAndImplementIDomainEvent()
    {
        var domainEvents = Types.InAssembly(Assemblies.Domain)
            .That()
            .ImplementInterface(typeof(IDomainEvent));
            
        domainEvents.GetTypes().Should().NotBeEmpty();

        var result = domainEvents
            .Should()
            .BeSealed()
            .And()
            .HaveNameEndingWith("DomainEvent")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }
}