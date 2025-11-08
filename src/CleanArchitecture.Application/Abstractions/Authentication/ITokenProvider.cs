using CleanArchitecture.Domain.Users;

namespace CleanArchitecture.Application.Abstractions.Authentication;

public interface ITokenProvider
{
    (string Jti, string AccessToken) CreateAccessToken(User user);
    string CreateRefreshToken();
}