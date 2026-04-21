using System.Text.Json;
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
            detail: result.Error.Localize(localizer),
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
                ErrorType.TooManyRequests => "Too Many Request",
                _ => "Server failure"
            };

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
                ErrorType.TooManyRequests => StatusCodes.Status429TooManyRequests,
                _ => StatusCodes.Status500InternalServerError
            };

        static Dictionary<string, object?>? GetErrors(Result result, IStringLocalizer<Lang> localizer)
        {
            var errors = result.Error switch
            {
                ValidationError v => v.Errors
                    .Select(e => new ErrorDetail(NormalizeField(e.Field), e.Localize(localizer)))
                    .ToArray(),

                var err when NormalizeField(err.Field) is { } field =>
                    new[] { new ErrorDetail(field, err.Localize(localizer)) },

                _ => null
            };

            return errors is not null
                ? new Dictionary<string, object?> { ["errors"] = errors }
                : null;
        }
    }

    private record ErrorDetail(string? Field, string Message);

    private static string Localize(this Error error, IStringLocalizer<Lang> localizer)
    {
        if (!Enum.IsDefined(error.Type) || error.Type == ErrorType.None)
            return localizer[Error.Unexpected.ErrorCode].Value;

        var template = localizer[error.ErrorCode].Value;

        if (error.Args is not { Count: > 0 })
            return template;

        return error.Args.Aggregate(template, (current, arg) =>
            current.Replace($"{{{arg.Key}}}", arg.Value?.ToString()));
    }

    private static string? NormalizeField(string? field)
    {
        if (string.IsNullOrWhiteSpace(field))
        {
            return null;
        }

        return string.Join(
            ".",
            field.Split('.')
                .Select(static segment =>
                {
                    var indexStart = segment.IndexOf('[');
                    if (indexStart < 0)
                    {
                        return JsonNamingPolicy.CamelCase.ConvertName(segment);
                    }

                    var propertyName = segment[..indexStart];
                    var suffix = segment[indexStart..];

                    return string.IsNullOrEmpty(propertyName)
                        ? segment
                        : $"{JsonNamingPolicy.CamelCase.ConvertName(propertyName)}{suffix}";
                }));
    }
}