using ClaimsIntake.Application.Claims.Commands.UpdateClaimStatus;
using FluentValidation.TestHelper;

namespace ClaimsIntake.Tests.Application.Validators;

public class UpdateClaimStatusCommandValidatorTests
{
    private readonly UpdateClaimStatusCommandValidator _validator = new();

    [Theory]
    [InlineData("Denied")]
    [InlineData("AdditionalInfoRequired")]
    public void NotesMissing_WhenStatusRequiresThem_Fails(string status)
    {
        var command = new UpdateClaimStatusCommand(Guid.NewGuid(), status, ReviewNotes: null);

        _validator.TestValidate(command)
            .ShouldHaveValidationErrorFor(c => c.ReviewNotes)
            .WithErrorMessage("Review notes are required when denying a claim or requesting additional information.");
    }

    [Theory]
    [InlineData("Denied")]
    [InlineData("AdditionalInfoRequired")]
    public void NotesPresent_WhenStatusRequiresThem_Passes(string status)
    {
        var command = new UpdateClaimStatusCommand(Guid.NewGuid(), status, "Explained to claimant.");

        _validator.TestValidate(command).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("Approved")]
    [InlineData("UnderReview")]
    public void NotesAbsent_WhenStatusDoesNotRequireThem_Passes(string status)
    {
        var command = new UpdateClaimStatusCommand(Guid.NewGuid(), status, ReviewNotes: null);

        _validator.TestValidate(command).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void UnparseableStatus_Fails()
    {
        var command = new UpdateClaimStatusCommand(Guid.NewGuid(), "Banana", null);

        _validator.TestValidate(command)
            .ShouldHaveValidationErrorFor(c => c.NewStatus);
    }

    [Fact]
    public void EmptyClaimId_Fails()
    {
        var command = new UpdateClaimStatusCommand(Guid.Empty, "Approved", null);

        _validator.TestValidate(command)
            .ShouldHaveValidationErrorFor(c => c.ClaimId);
    }
}
