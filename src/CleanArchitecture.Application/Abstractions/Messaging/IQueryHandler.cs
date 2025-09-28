using CleanArchitecture.Shared;
using MediatR;

namespace CleanArchitecture.Application.Abstractions.Messaging;

public interface IQueryHandler<in TQuery, TResponse>
    : IRequestHandler<TQuery, Result<TResponse>> where TQuery : IQuery<TResponse>;