using FluentValidation;
using MediatR;

namespace ClaimsIntake.Application.Common.Behaviours;

/// <summary>
/// Runs all registered <see cref="IValidator{T}"/> instances for a request before its handler.
/// Failures are thrown as a <see cref="ValidationException"/>, which the API layer maps to HTTP 422 —
/// handlers and endpoints never invoke validation themselves.
/// </summary>
public sealed class ValidationBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    /// <summary>Receives every validator registered for <typeparamref name="TRequest"/> (possibly none).</summary>
    public ValidationBehaviour(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    /// <summary>Validates the request with all registered validators, then invokes the next pipeline step.</summary>
    /// <exception cref="ValidationException">One or more validators reported failures.</exception>
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (_validators.Any())
        {
            var context = new ValidationContext<TRequest>(request);

            var results = await Task.WhenAll(
                _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

            var failures = results
                .SelectMany(r => r.Errors)
                .Where(f => f is not null)
                .ToList();

            if (failures.Count != 0)
                throw new ValidationException(failures);
        }

        return await next();
    }
}
