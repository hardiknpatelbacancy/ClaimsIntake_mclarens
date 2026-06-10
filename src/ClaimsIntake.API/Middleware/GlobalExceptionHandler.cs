using System.Text.Json;
using ClaimsIntake.Application.Common.Exceptions;
using ClaimsIntake.Domain.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ClaimsIntake.API.Middleware;

/// <summary>
/// Single place where application and domain exceptions become RFC 7807 responses:
/// FluentValidation → 422, domain rule violations → 409, missing resources → 404,
/// everything else → 500 (with detail only outside production).
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly IHostEnvironment _environment;
    private readonly ILogger<GlobalExceptionHandler> _logger;

    /// <summary>Creates the handler; the environment decides whether 500 responses expose exception detail.</summary>
    public GlobalExceptionHandler(IHostEnvironment environment, ILogger<GlobalExceptionHandler> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    /// <summary>
    /// Maps the exception to a <see cref="ProblemDetails"/> response per the table in the class summary
    /// and writes it as <c>application/problem+json</c>. Always handles the exception (returns <c>true</c>).
    /// </summary>
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ProblemDetails problemDetails = exception switch
        {
            ValidationException validationException => CreateValidationProblem(validationException),

            DomainException domainException => new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.10",
                Title = "Business Rule Violated",
                Status = StatusCodes.Status409Conflict,
                Detail = domainException.Message
            },

            NotFoundException notFoundException => new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.5",
                Title = "Resource Not Found",
                Status = StatusCodes.Status404NotFound,
                Detail = notFoundException.Message
            },

            _ => CreateInternalServerErrorProblem(exception)
        };

        problemDetails.Instance = httpContext.Request.Path;

        httpContext.Response.StatusCode = problemDetails.Status!.Value;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, problemDetails.GetType(), options: null,
            contentType: "application/problem+json", cancellationToken);

        return true;
    }

    /// <summary>Builds the 422 problem, grouping failures into an <c>errors</c> dictionary keyed by camelCase field name.</summary>
    private static HttpValidationProblemDetails CreateValidationProblem(ValidationException exception)
    {
        var errors = exception.Errors
            .GroupBy(failure => failure.PropertyName)
            .ToDictionary(
                group => JsonNamingPolicy.CamelCase.ConvertName(group.Key),
                group => group.Select(failure => failure.ErrorMessage).ToArray());

        return new HttpValidationProblemDetails(errors)
        {
            Type = "https://tools.ietf.org/html/rfc4918#section-11.2",
            Title = "Validation Failed",
            Status = StatusCodes.Status422UnprocessableEntity
        };
    }

    /// <summary>Builds the 500 problem, logging the exception and suppressing its message in Production.</summary>
    private ProblemDetails CreateInternalServerErrorProblem(Exception exception)
    {
        _logger.LogError(exception, "Unhandled exception");

        return new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc9110#section-15.6.1",
            Title = "An unexpected error occurred.",
            Status = StatusCodes.Status500InternalServerError,
            Detail = _environment.IsProduction() ? null : exception.Message
        };
    }
}
