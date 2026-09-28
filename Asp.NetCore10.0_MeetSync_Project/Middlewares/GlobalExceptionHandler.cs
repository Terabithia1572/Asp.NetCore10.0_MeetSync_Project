using MeetSync.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Asp.NetCore10._0_MeetSync_Project.Middlewares;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(
            exception,
            "An unhandled exception occurred during request execution: {Message} [TraceId: {TraceId}]",
            exception.Message,
            httpContext.TraceIdentifier);

        var (statusCode, title, detail, errors) = exception switch
        {
            ValidationException valEx => (
                StatusCodes.Status400BadRequest,
                "Validation Error",
                valEx.Message,
                valEx.Errors
            ),
            NotFoundException nfEx => (
                StatusCodes.Status404NotFound,
                "Resource Not Found",
                nfEx.Message,
                null
            ),
            BusinessRuleException bizEx => (
                StatusCodes.Status400BadRequest,
                "Business Rule Violation",
                bizEx.Message,
                null
            ),
            DomainException domEx => (
                StatusCodes.Status400BadRequest,
                "Domain Exception",
                domEx.Message,
                null
            ),
            UnauthorizedAccessException => (
                StatusCodes.Status401Unauthorized,
                "Unauthorized Access",
                "You are not authorized to perform this operation.",
                null
            ),
            KeyNotFoundException => (
                StatusCodes.Status404NotFound,
                "Key Not Found",
                "The specified key or resource was not found.",
                null
            ),
            _ => (
                StatusCodes.Status500InternalServerError,
                "Internal Server Error",
                "An unexpected server error occurred. Please try again later.",
                null
            )
        };

        // Determine if request expects API/JSON response
        bool isApiRequest = httpContext.Request.Path.StartsWithSegments("/api") ||
                           httpContext.Request.Headers.Accept.ToString().Contains("application/json") ||
                           httpContext.Request.Headers["X-Requested-With"] == "XMLHttpRequest";

        if (isApiRequest)
        {
            var problemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = detail,
                Instance = httpContext.Request.Path
            };

            problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

            if (errors is not null)
            {
                problemDetails.Extensions["errors"] = errors;
            }

            httpContext.Response.StatusCode = statusCode;
            httpContext.Response.ContentType = "application/problem+json";
            await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
            return true;
        }

        // For non-API/HTML MVC requests, let default error handler handle or format response
        httpContext.Response.StatusCode = statusCode;
        return false;
    }
}
