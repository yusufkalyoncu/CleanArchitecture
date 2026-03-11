namespace CleanArchitecture.Shared;

public abstract class Entity
{
    public Guid Id { get; } = Guid.NewGuid();
}