using CleanArchitecture.Shared;
using CleanArchitecture.Shared.Resources.Languages;
using Microsoft.Extensions.Localization;

namespace CleanArchitecture.WebApi.Extensions;

public static class ResultExtensions
{
    public static IResult ToOk(this Result result, IStringLocalizer<Lang> localizer) =>
        result.IsSuccess
            ? Results.Ok()
            : result.ToProblemDetails(localizer);

    public static IResult ToOk<T>(this Result<T> result, IStringLocalizer<Lang> localizer) =>
        result.IsSuccess
            ? Results.Ok(result.Data)
            : result.ToProblemDetails(localizer);
    
    public static IResult ToNoContent(this Result result, IStringLocalizer<Lang> localizer) =>
        result.IsSuccess
            ? Results.NoContent()
            : result.ToProblemDetails(localizer);
    
    private static IResult ToProblemDetails(this Result result, IStringLocalizer<Lang> localizer)
    {
        if (result.IsSuccess)
        {
            throw new InvalidOperationException();
        }

        return Results.Problem(
            title: GetTitle(result.Error),
            detail: GetDetail(result.Error, localizer),
            type: GetType(result.Error.Type),
            statusCode: GetStatusCode(result.Error.Type),
            extensions: GetErrors(result, localizer));

        static string GetTitle(Error error) =>
            error.Type switch
            {
                ErrorType.BadRequest => "Bad Request",
                ErrorType.Unauthorized => "Unauthorized",
                ErrorType.Forbidden => "Forbidden",
                ErrorType.NotFound => "Not Found",
                ErrorType.Conflict => "Conflict",
                _ => "Server failure"
            };

        static string GetDetail(Error error, IStringLocalizer<Lang> localizer)
        {
            return error.Type switch
            {
                ErrorType.BadRequest => localizer[error.ErrorCode],
                ErrorType.Unauthorized => localizer[error.ErrorCode],
                ErrorType.Forbidden => localizer[error.ErrorCode],
                ErrorType.NotFound => localizer[error.ErrorCode],
                ErrorType.Conflict => localizer[error.ErrorCode],
                _ => localizer["Error.Unexpected"]
            };
        }

        static string GetType(ErrorType errorType) =>
            errorType switch
            {
                ErrorType.BadRequest   => "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                ErrorType.Unauthorized => "https://tools.ietf.org/html/rfc7235#section-3.1",
                ErrorType.Forbidden    => "https://tools.ietf.org/html/rfc7231#section-6.5.3",
                ErrorType.NotFound     => "https://tools.ietf.org/html/rfc7231#section-6.5.4",
                ErrorType.Conflict     => "https://tools.ietf.org/html/rfc7231#section-6.5.8",
                _ => "https://tools.ietf.org/html/rfc7231#section-6.6.1"
            };
        
        static int GetStatusCode(ErrorType errorType) =>
            errorType switch
            {
                ErrorType.BadRequest => StatusCodes.Status400BadRequest,
                ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
                ErrorType.Forbidden => StatusCodes.Status403Forbidden,
                ErrorType.NotFound => StatusCodes.Status404NotFound,
                ErrorType.Conflict => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status500InternalServerError
            };

        static Dictionary<string, object?>? GetErrors(Result result, IStringLocalizer<Lang> localizer)
        {
            if (result.Error is not ValidationError validationError)
            {
                return null;
            }
            
            var localizedErrors = validationError.Errors
                .Select(e => new
                {
                    Code = e.ErrorCode,
                    Description = localizer[e.ErrorCode]
                })
                .ToArray();

            return new Dictionary<string, object?>
            {
                { "errors", localizedErrors }
            };
        }
    }
}