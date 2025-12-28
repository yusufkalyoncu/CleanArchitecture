using CleanArchitecture.Shared;

namespace CleanArchitecture.Application.Users.Register.Events;

public sealed record UserRegisteredIntegrationEvent(Guid UserId) : IIntegrationEvent;