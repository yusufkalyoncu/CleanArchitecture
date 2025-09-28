using CleanArchitecture.Shared;
using MediatR;

namespace CleanArchitecture.Application.Abstractions.Messaging;

public interface ICommandHandler<in TCommand>
    : IRequestHandler<TCommand, Result>
    where TCommand : ICommand;

public interface ICommandHandler<in TCommand, TResponse>
    : IRequestHandler<TCommand, Result<TResponse>>
    where TCommand : ICommand<TResponse>;