namespace CleanArchitecture.Shared;

public enum ErrorType
{
    None = 200,
    BadRequest = 400,
    Unauthorized = 401,
    Forbidden = 403,
    NotFound = 404,
    Conflict = 409
}