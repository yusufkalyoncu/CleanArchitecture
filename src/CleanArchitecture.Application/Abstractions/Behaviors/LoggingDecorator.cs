using CleanArchitecture.Application.Abstractions.Messaging;
using CleanArchitecture.Shared;
using Microsoft.Extensions.Logging;

namespace CleanArchitecture.Application.Abstractions.Behaviors;

internal static class LoggingDecorator
{
    internal sealed class CommandHandler<TCommand, TResponse>(
        ICommandHandler<TCommand, TResponse> innerHandler,
        ILogger<CommandHandler<TCommand, TResponse>> logger)
        : ICommandHandler<TCommand, TResponse>
        where TCommand : ICommand<TResponse>
    {
        private static readonly string CommandName = typeof(TCommand).Name;

        public async Task<Result<TResponse>> Handle(TCommand command, CancellationToken cancellationToken)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Processing command {Command}", CommandName);
            }

            Result<TResponse> result = await innerHandler.Handle(command, cancellationToken);

            if (result.IsSuccess)
            {
                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Completed command {Command}", CommandName);
                }
            }
            else
            {
                logger.LogError("Failed command {Command} with error ({StatusCode}): {ErrorMessage}",
                    CommandName,
                    result.Error.Type,
                    result.Error.ErrorCode);
            }

            return result;
        }
    }

    internal sealed class CommandBaseHandler<TCommand>(
        ICommandHandler<TCommand> innerHandler,
        ILogger<CommandBaseHandler<TCommand>> logger)
        : ICommandHandler<TCommand>
        where TCommand : ICommand
    {
        private static readonly string CommandName = typeof(TCommand).Name;

        public async Task<Result> Handle(TCommand command, CancellationToken cancellationToken)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Processing command {Command}", CommandName);
            }

            Result result = await innerHandler.Handle(command, cancellationToken);

            if (result.IsSuccess)
            {
                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Completed command {Command}", CommandName);
                }
            }
            else
            {
                logger.LogError("Failed command {Command} with error ({StatusCode}): {ErrorMessage}",
                    CommandName,
                    result.Error.Type,
                    result.Error.ErrorCode);
            }

            return result;
        }
    }

    internal sealed class QueryHandler<TQuery, TResponse>(
        IQueryHandler<TQuery, TResponse> innerHandler,
        ILogger<QueryHandler<TQuery, TResponse>> logger)
        : IQueryHandler<TQuery, TResponse>
        where TQuery : IQuery<TResponse>
    {
        private static readonly string QueryName = typeof(TQuery).Name;

        public async Task<Result<TResponse>> Handle(TQuery query, CancellationToken cancellationToken)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Processing query {Query}", QueryName);
            }

            Result<TResponse> result = await innerHandler.Handle(query, cancellationToken);

            if (result.IsSuccess)
            {
                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Completed query {Query}", QueryName);
                }
            }
            else
            {
                logger.LogError("Failed query {Query} with error ({StatusCode}): {ErrorMessage}",
                    QueryName,
                    result.Error.Type,
                    result.Error.ErrorCode);
            }

            return result;
        }
    }
}