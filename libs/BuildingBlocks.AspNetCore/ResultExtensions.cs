using BuildingBlocks.Common;
using Microsoft.AspNetCore.Http;

namespace BuildingBlocks.AspNetCore;

public static class ResultExtensions
{
    public static IResult ToProblem(this Error error)
    {
        var statusCode = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status422UnprocessableEntity,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.TooManyRequests => StatusCodes.Status429TooManyRequests,
            ErrorType.Unavailable => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status500InternalServerError
        };

        return TypedResults.Problem(
            title: error.Code,
            detail: error.Message,
            statusCode: statusCode);
    }

    public static IResult ToHttpResult<T>(this Result<T> result) =>
        result.Match(value => TypedResults.Ok(value), error => error.ToProblem());

    public static IResult ToHttpResult(this Result result)
    {
        if (result.IsFailure)
            return result.Error.Value.ToProblem();

        return TypedResults.NoContent();
    }
}