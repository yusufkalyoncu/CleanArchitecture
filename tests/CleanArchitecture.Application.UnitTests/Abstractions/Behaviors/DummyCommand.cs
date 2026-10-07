using CleanArchitecture.Application.Abstractions.Messaging;

namespace CleanArchitecture.Application.UnitTests.Abstractions.Behaviors;

public sealed record DummyCommand : ICommand<string>;