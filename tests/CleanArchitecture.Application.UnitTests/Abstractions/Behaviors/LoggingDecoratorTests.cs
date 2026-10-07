using CleanArchitecture.Application.Abstractions.Behaviors;
using CleanArchitecture.Application.Abstractions.Messaging;
using CleanArchitecture.Shared;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CleanArchitecture.Application.UnitTests.Abstractions.Behaviors;

public class LoggingDecoratorTests
{
    private sealed record DummyCommand : ICommand<string>;

    [Fact]
    public async Task Handle_ShouldCallInnerHandlerAndReturnResult()
    {
        // Arrange
        var innerHandler = Substitute.For<ICommandHandler<DummyCommand, string>>();
        innerHandler.Handle(Arg.Any<DummyCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success("success-data"));

        var logger = NullLogger<LoggingDecorator.CommandHandler<DummyCommand, string>>.Instance;

        var decorator = new LoggingDecorator.CommandHandler<DummyCommand, string>(
            innerHandler,
            logger);

        // Act
        var result = await decorator.Handle(new DummyCommand(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Be("success-data");
        await innerHandler.Received(1).Handle(Arg.Any<DummyCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenInnerHandlerFails_ShouldReturnFailure()
    {
        // Arrange
        var innerHandler = Substitute.For<ICommandHandler<DummyCommand, string>>();
        var error = Error.NotFound("Dummy.NotFound");
        innerHandler.Handle(Arg.Any<DummyCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<string>(error));

        var logger = NullLogger<LoggingDecorator.CommandHandler<DummyCommand, string>>.Instance;

        var decorator = new LoggingDecorator.CommandHandler<DummyCommand, string>(
            innerHandler,
            logger);

        // Act
        var result = await decorator.Handle(new DummyCommand(), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
        await innerHandler.Received(1).Handle(Arg.Any<DummyCommand>(), Arg.Any<CancellationToken>());
    }
}