using System.Collections.Concurrent;
using System.Reflection;
using CleanArchitecture.Application.Abstractions.EventBus;
using CleanArchitecture.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace CleanArchitecture.Infrastructure.EventBus;

public sealed class InMemoryEventBus(
    IServiceProvider serviceProvider,
    IntegrationEventHandlerRegistry handlerRegistry) : IEventBus
{
    private static readonly ConcurrentDictionary<Type, MethodInfo> HandleMethodCache = new();

    public async Task PublishAsync(
        IIntegrationEvent integrationEvent,
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        var eventType = integrationEvent.GetType();
        var handlerTypes = handlerRegistry.GetHandlerTypes(eventType);

        if (handlerTypes.Length == 0)
            return;

        var handleMethod = HandleMethodCache.GetOrAdd(
            eventType,
            et => typeof(IIntegrationEventHandler<>)
                      .MakeGenericType(et)
                      .GetMethod("Handle")!);

        var exceptions = new List<Exception>();
        OperationCanceledException? shutdownException = null;

        foreach (var handlerType in handlerTypes)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                shutdownException ??= new OperationCanceledException(cancellationToken);
                break;
            }

            try
            {
                await InvokeHandlerInScopeAsync(
                    serviceProvider, integrationEvent, handlerType, handleMethod, messageId, cancellationToken);
            }
            catch (OperationCanceledException oce) when (cancellationToken.IsCancellationRequested)
            {
                shutdownException = oce;
                break;
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        }

        if (exceptions.Count > 0)
        {
            if (exceptions.Count == 1)
                System.Runtime.ExceptionServices.ExceptionDispatchInfo
                    .Capture(exceptions[0])
                    .Throw();

            throw new AggregateException(
                $"One or more handlers failed for event '{eventType.Name}'.", exceptions);
        }

        if (shutdownException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo
                .Capture(shutdownException)
                .Throw();
        }
    }

    private static async Task InvokeHandlerInScopeAsync(
        IServiceProvider rootProvider,
        object integrationEvent,
        Type handlerType,
        MethodInfo handleMethod,
        Guid messageId,
        CancellationToken ct)
    {
        await using var scope = rootProvider.CreateAsyncScope();

        var context = scope.ServiceProvider.GetRequiredService<MessageContext>();
        context.MessageId = messageId;

        var handler = scope.ServiceProvider.GetRequiredService(handlerType);

        try
        {
            await (Task)handleMethod.Invoke(handler, [integrationEvent, ct])!;
        }
        catch (TargetInvocationException tie) when (tie.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo
                .Capture(tie.InnerException)
                .Throw();
        }
    }
}