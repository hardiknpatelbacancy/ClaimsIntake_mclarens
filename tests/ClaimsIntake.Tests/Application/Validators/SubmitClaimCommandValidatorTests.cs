using ClaimsIntake.Application.Claims.Commands.SubmitClaim;
using FluentValidation.TestHelper;

namespace ClaimsIntake.Tests.Application.Validators;

public class SubmitClaimCommandValidatorTests
{
    private readonly SubmitClaimCommandValidator _validator = new();

    private static SubmitClaimCommand ValidCommand(
        string policyNumber = "POL-1001",
        DateOnly? incidentDate = null) =>
        new(policyNumber, "Jane Doe", "Rear-end collision", incidentDate ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7)));

    [Fact]
    public void ValidCommand_Passes()
    {
        _validator.TestValidate(ValidCommand()).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyPolicyNumber_Fails()
    {
        _validator.TestValidate(ValidCommand(policyNumber: ""))
            .ShouldHaveValidationErrorFor(c => c.PolicyNumber);
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("POL-")]
    [InlineData("POL-123")]      // fewer than 4 digits
    [InlineData("pol-1001")]     // lowercase prefix
    [InlineData("POL-12345678901")] // more than 10 digits
    public void PolicyNumberNotMatchingPattern_Fails(string policyNumber)
    {
        _validator.TestValidate(ValidCommand(policyNumber: policyNumber))
            .ShouldHaveValidationErrorFor(c => c.PolicyNumber);
    }

    [Fact]
    public void FutureIncidentDate_Fails()
    {
        _validator.TestValidate(ValidCommand(incidentDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2))))
            .ShouldHaveValidationErrorFor(c => c.IncidentDate);
    }

    [Fact]
    public void IncidentDateOlderThanTwoYears_Fails()
    {
        _validator.TestValidate(ValidCommand(incidentDate: DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-2).AddDays(-1))))
            .ShouldHaveValidationErrorFor(c => c.IncidentDate)
            .WithErrorMessage("Incident date cannot be more than 2 years in the past.");
    }

    [Fact]
    public void IncidentDateWithinTwoYearWindow_Passes()
    {
        _validator.TestValidate(ValidCommand(incidentDate: DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-1))))
            .ShouldNotHaveValidationErrorFor(c => c.IncidentDate);
    }
}
