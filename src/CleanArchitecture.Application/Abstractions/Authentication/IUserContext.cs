namespace CleanArchitecture.Application.Abstractions.Authentication;

public interface IUserContext
{
    public bool IsAuthenticated { get; }
    public Guid Id { get; }
    public string Jti { get; }
    public TimeSpan AccessTokenRemainingLifetime { get; }
    public string? IpAddress { get; }
    public string? UserAgent { get; }
}