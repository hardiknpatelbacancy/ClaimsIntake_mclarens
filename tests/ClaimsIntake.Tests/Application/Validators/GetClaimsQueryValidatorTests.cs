using ClaimsIntake.Application.Claims.Queries.GetClaims;
using FluentValidation.TestHelper;

namespace ClaimsIntake.Tests.Application.Validators;

public class GetClaimsQueryValidatorTests
{
    private readonly GetClaimsQueryValidator _validator = new();

    private static GetClaimsQuery Query(
        string? status = null,
        DateTime? from = null,
        DateTime? to = null,
        int pageNumber = 1,
        int pageSize = 20) =>
        new(status, null, from, to, pageNumber, pageSize);

    [Fact]
    public void DefaultQuery_Passes()
    {
        _validator.TestValidate(Query()).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(101, false)]
    [InlineData(1, true)]
    [InlineData(50, true)]
    [InlineData(100, true)]
    public void PageSizeBounds(int pageSize, bool valid)
    {
        var result = _validator.TestValidate(Query(pageSize: pageSize));

        if (valid)
            result.ShouldNotHaveValidationErrorFor(q => q.PageSize);
        else
            result.ShouldHaveValidationErrorFor(q => q.PageSize);
    }

    [Fact]
    public void PageNumberZero_Fails()
    {
        _validator.TestValidate(Query(pageNumber: 0))
            .ShouldHaveValidationErrorFor(q => q.PageNumber);
    }

    [Fact]
    public void SubmittedFromAfterSubmittedTo_Fails()
    {
        var result = _validator.TestValidate(Query(from: new DateTime(2026, 6, 10), to: new DateTime(2026, 6, 1)));

        result.ShouldHaveValidationErrorFor(q => q.SubmittedFrom);
    }

    [Fact]
    public void EqualFromAndTo_Passes()
    {
        var date = new DateTime(2026, 6, 10);

        _validator.TestValidate(Query(from: date, to: date))
            .ShouldNotHaveValidationErrorFor(q => q.SubmittedFrom);
    }

    [Fact]
    public void InvalidStatusName_Fails()
    {
        _validator.TestValidate(Query(status: "Nope"))
            .ShouldHaveValidationErrorFor(q => q.Status);
    }

    [Fact]
    public void AbsentStatus_Passes()
    {
        _validator.TestValidate(Query(status: null))
            .ShouldNotHaveValidationErrorFor(q => q.Status);
    }
}
