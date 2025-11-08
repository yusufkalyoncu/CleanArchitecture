using CleanArchitecture.Shared;

namespace CleanArchitecture.Application.Abstractions.Messaging;

public interface ICommandHandler<in TCommand>
    where TCommand : ICommand
{
    Task<Result> Handle(TCommand request, CancellationToken cancellationToken);
}

public interface ICommandHandler<in TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    Task<Result<TResponse>> Handle(TCommand request, CancellationToken cancellationToken);
}