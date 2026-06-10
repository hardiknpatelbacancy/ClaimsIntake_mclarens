using ClaimsIntake.Domain.Entities;
using ClaimsIntake.Domain.Enums;
using ClaimsIntake.Domain.Exceptions;

namespace ClaimsIntake.Tests.Domain;

public class ClaimTests
{
    private static Claim SubmitValidClaim() =>
        Claim.Submit("POL-1001", "Jane Doe", "Rear-end collision", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7)));

    /// <summary>Walks a fresh claim to the requested state using only legal transitions.</summary>
    private static Claim ClaimIn(ClaimStatus status)
    {
        var claim = SubmitValidClaim();
        switch (status)
        {
            case ClaimStatus.Submitted:
                break;
            case ClaimStatus.UnderReview:
                claim.TransitionTo(ClaimStatus.UnderReview);
                break;
            case ClaimStatus.AdditionalInfoRequired:
                claim.TransitionTo(ClaimStatus.UnderReview);
                claim.TransitionTo(ClaimStatus.AdditionalInfoRequired, "need photos");
                break;
            case ClaimStatus.Approved:
                claim.TransitionTo(ClaimStatus.UnderReview);
                claim.TransitionTo(ClaimStatus.Approved);
                break;
            case ClaimStatus.Denied:
                claim.TransitionTo(ClaimStatus.UnderReview);
                claim.TransitionTo(ClaimStatus.Denied, "not covered");
                break;
        }
        return claim;
    }

    [Fact]
    public void Submit_SetsInitialState()
    {
        var before = DateTime.UtcNow;
        var claim = SubmitValidClaim();
        var after = DateTime.UtcNow;

        Assert.NotEqual(Guid.Empty, claim.Id);
        Assert.Equal(ClaimStatus.Submitted, claim.Status);
        Assert.Equal("POL-1001", claim.PolicyNumber);
        Assert.InRange(claim.SubmittedAt, before, after);
        Assert.Equal(claim.SubmittedAt, claim.LastUpdatedAt);
        Assert.Null(claim.ReviewNotes);
    }

    [Theory]
    [InlineData("", "Jane", "desc")]
    [InlineData("  ", "Jane", "desc")]
    [InlineData("POL-1001", "", "desc")]
    [InlineData("POL-1001", "Jane", "")]
    public void Submit_RejectsMissingRequiredFields(string policyNumber, string claimantName, string description)
    {
        Assert.Throws<ArgumentException>(() =>
            Claim.Submit(policyNumber, claimantName, description, DateOnly.FromDateTime(DateTime.UtcNow)));
    }

    [Fact]
    public void Submit_RejectsFutureIncidentDate()
    {
        Assert.Throws<ArgumentException>(() =>
            Claim.Submit("POL-1001", "Jane", "desc", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2))));
    }

    [Theory]
    [InlineData(ClaimStatus.Submitted, ClaimStatus.UnderReview)]
    [InlineData(ClaimStatus.UnderReview, ClaimStatus.AdditionalInfoRequired)]
    [InlineData(ClaimStatus.UnderReview, ClaimStatus.Approved)]
    [InlineData(ClaimStatus.UnderReview, ClaimStatus.Denied)]
    [InlineData(ClaimStatus.AdditionalInfoRequired, ClaimStatus.UnderReview)]
    public void TransitionTo_AllowsLegalTransitions(ClaimStatus from, ClaimStatus to)
    {
        var claim = ClaimIn(from);

        claim.TransitionTo(to);

        Assert.Equal(to, claim.Status);
    }

    // Full complement of the 5 legal pairs: every other (from, to) combination must throw.
    [Theory]
    [InlineData(ClaimStatus.Submitted, ClaimStatus.Submitted)]
    [InlineData(ClaimStatus.Submitted, ClaimStatus.AdditionalInfoRequired)]
    [InlineData(ClaimStatus.Submitted, ClaimStatus.Approved)]
    [InlineData(ClaimStatus.Submitted, ClaimStatus.Denied)]
    [InlineData(ClaimStatus.UnderReview, ClaimStatus.Submitted)]
    [InlineData(ClaimStatus.UnderReview, ClaimStatus.UnderReview)]
    [InlineData(ClaimStatus.AdditionalInfoRequired, ClaimStatus.Submitted)]
    [InlineData(ClaimStatus.AdditionalInfoRequired, ClaimStatus.AdditionalInfoRequired)]
    [InlineData(ClaimStatus.AdditionalInfoRequired, ClaimStatus.Approved)]
    [InlineData(ClaimStatus.AdditionalInfoRequired, ClaimStatus.Denied)]
    [InlineData(ClaimStatus.Approved, ClaimStatus.Submitted)]
    [InlineData(ClaimStatus.Approved, ClaimStatus.UnderReview)]
    [InlineData(ClaimStatus.Approved, ClaimStatus.AdditionalInfoRequired)]
    [InlineData(ClaimStatus.Approved, ClaimStatus.Approved)]
    [InlineData(ClaimStatus.Approved, ClaimStatus.Denied)]
    [InlineData(ClaimStatus.Denied, ClaimStatus.Submitted)]
    [InlineData(ClaimStatus.Denied, ClaimStatus.UnderReview)]
    [InlineData(ClaimStatus.Denied, ClaimStatus.AdditionalInfoRequired)]
    [InlineData(ClaimStatus.Denied, ClaimStatus.Approved)]
    [InlineData(ClaimStatus.Denied, ClaimStatus.Denied)]
    public void TransitionTo_ThrowsOnIllegalTransitions(ClaimStatus from, ClaimStatus to)
    {
        var claim = ClaimIn(from);

        var ex = Assert.Throws<InvalidClaimStateTransitionException>(() => claim.TransitionTo(to));

        Assert.Equal(from, ex.From);
        Assert.Equal(to, ex.To);
        Assert.Equal(from, claim.Status); // state unchanged after a rejected transition
    }

    [Fact]
    public void TransitionTo_WithNotes_SetsReviewNotesAndTouchesLastUpdatedAt()
    {
        var claim = SubmitValidClaim();
        var submittedAt = claim.LastUpdatedAt;

        claim.TransitionTo(ClaimStatus.UnderReview, "Assigned to adjuster.");

        Assert.Equal("Assigned to adjuster.", claim.ReviewNotes);
        Assert.True(claim.LastUpdatedAt >= submittedAt);
    }

    [Fact]
    public void TransitionTo_WithWhitespaceNotes_KeepsPreviousNotes()
    {
        var claim = SubmitValidClaim();
        claim.TransitionTo(ClaimStatus.UnderReview, "First note.");

        claim.TransitionTo(ClaimStatus.AdditionalInfoRequired, "   ");

        Assert.Equal("First note.", claim.ReviewNotes);
    }
}
