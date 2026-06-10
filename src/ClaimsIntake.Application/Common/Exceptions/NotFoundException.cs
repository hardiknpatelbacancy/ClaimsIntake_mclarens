namespace ClaimsIntake.Application.Common.Exceptions;

public sealed class NotFoundException : Exception
{
    /// <summary>Creates the exception for a missing <paramref name="entityName"/> identified by <paramref name="key"/>.</summary>
    public NotFoundException(string entityName, object key)
        : base($"{entityName} with id '{key}' was not found.")
    {
    }
}
