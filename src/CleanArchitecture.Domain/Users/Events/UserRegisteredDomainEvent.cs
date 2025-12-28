using CleanArchitecture.Shared;

namespace CleanArchitecture.Domain.Users.Events;

public sealed record UserRegisteredDomainEvent(Guid UserId) : IDomainEvent;