using Microsoft.AspNetCore.Diagnostics;
using SyncBingo.Domain;

namespace SyncBingo.Api.Http;

/// <summary>
/// Refusals as RFC 7807 problem details (docs/05 §5). Each <see cref="DomainErrorCode"/> maps to
/// one status, the message is safe to show to players, and <c>code</c> lets clients branch on
/// the reason without parsing text.
/// </summary>
public static class Problems
{
    public static int StatusOf(DomainErrorCode code) => code switch
    {
        DomainErrorCode.Unauthorized => StatusCodes.Status401Unauthorized,
        DomainErrorCode.Forbidden => StatusCodes.Status403Forbidden,
        DomainErrorCode.NotFound => StatusCodes.Status404NotFound,
        DomainErrorCode.WrongState or DomainErrorCode.HopperEmpty or DomainErrorCode.SessionFull or DomainErrorCode.Conflict
            => StatusCodes.Status409Conflict,
        DomainErrorCode.NotAllowed or DomainErrorCode.Invalid => StatusCodes.Status422UnprocessableEntity,
        _ => StatusCodes.Status500InternalServerError,
    };

    public static IResult From(DomainError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return TypedResults.Problem(
            detail: error.Message,
            statusCode: StatusOf(error.Code),
            extensions: new Dictionary<string, object?> { ["code"] = error.Code.ToString() });
    }

    /// <summary>The value as <paramref name="ok"/>, or the refusal as a problem.</summary>
    public static IResult ToHttp<T>(this Result<T> result, Func<T, IResult> ok)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(ok);

        return result.IsSuccess ? ok(result.Value) : From(result.Error!);
    }

    /// <summary>
    /// Expected exceptions as problems: a body that doesn't parse is a 400 (in every environment),
    /// and a session that stayed locked past the wait (a stuck or very busy game) a 503, not a 500.
    /// </summary>
    public sealed class ExceptionHandler(IProblemDetailsService problems) : IExceptionHandler
    {
        public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(httpContext);

            var (status, detail) = exception switch
            {
                BadHttpRequestException bad => (bad.StatusCode, "The request isn't valid. Check its fields and values."),
                TimeoutException => (StatusCodes.Status503ServiceUnavailable, "The game is busy. Please try again."),
                _ => (0, null),
            };
            if (detail is null)
            {
                return false;
            }

            httpContext.Response.StatusCode = status;
            return await problems.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = { Status = status, Detail = detail },
            }).ConfigureAwait(false);
        }
    }
}
