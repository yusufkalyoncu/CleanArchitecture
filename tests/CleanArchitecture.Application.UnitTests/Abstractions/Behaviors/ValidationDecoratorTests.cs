using CleanArchitecture.Application.Abstractions.Behaviors;
using CleanArchitecture.Application.Abstractions.Messaging;
using CleanArchitecture.Shared;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using NSubstitute;

namespace CleanArchitecture.Application.UnitTests.Abstractions.Behaviors;

public class ValidationDecoratorTests
{
    private sealed record DummyCommand : ICommand<string>;

    [Fact]
    public async Task Handle_WhenValidatorsAreEmpty_ShouldCallInnerHandler()
    {
        // Arrange
        var innerHandler = Substitute.For<ICommandHandler<DummyCommand, string>>();
        innerHandler.Handle(Arg.Any<DummyCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success("ok"));

        var decorator = new ValidationDecorator.CommandHandler<DummyCommand, string>(
            innerHandler,
            []);

        // Act
        var result = await decorator.Handle(new DummyCommand(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Be("ok");
        await innerHandler.Received(1).Handle(Arg.Any<DummyCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenAllValidatorsPass_ShouldCallInnerHandler()
    {
        // Arrange
        var innerHandler = Substitute.For<ICommandHandler<DummyCommand, string>>();
        innerHandler.Handle(Arg.Any<DummyCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success("ok"));

        var validator = Substitute.For<IValidator<DummyCommand>>();
        validator.ValidateAsync(Arg.Any<ValidationContext<DummyCommand>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult()); // valid

        var decorator = new ValidationDecorator.CommandHandler<DummyCommand, string>(
            innerHandler,
            [validator]);

        // Act
        var result = await decorator.Handle(new DummyCommand(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Be("ok");
        await innerHandler.Received(1).Handle(Arg.Any<DummyCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenValidationFails_ShouldReturnFailureAndNotCallInnerHandler()
    {
        // Arrange
        var innerHandler = Substitute.For<ICommandHandler<DummyCommand, string>>();

        var validator = Substitute.For<IValidator<DummyCommand>>();
        var failure = new ValidationFailure("TestProp", "Test error") { ErrorCode = "Test.Error" };
        validator.ValidateAsync(Arg.Any<ValidationContext<DummyCommand>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult([failure]));

        var decorator = new ValidationDecorator.CommandHandler<DummyCommand, string>(
            innerHandler,
            [validator]);

        // Act
        var result = await decorator.Handle(new DummyCommand(), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.BadRequest);
        result.Error.ErrorCode.Should().Be("General.Validation");

        await innerHandler.DidNotReceive().Handle(Arg.Any<DummyCommand>(), Arg.Any<CancellationToken>());
    }
}