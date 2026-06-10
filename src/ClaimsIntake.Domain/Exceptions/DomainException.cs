namespace ClaimsIntake.Domain.Exceptions;

public abstract class DomainException : Exception
{
    /// <summary>Creates the exception with a message describing the violated business rule.</summary>
    protected DomainException(string message) : base(message)
    {
    }
}
