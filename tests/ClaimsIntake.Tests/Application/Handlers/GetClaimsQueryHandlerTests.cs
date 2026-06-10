using ClaimsIntake.Application.Claims.Queries.GetClaims;
using ClaimsIntake.Domain.Enums;
using ClaimsIntake.Tests.Application.Fakes;

namespace ClaimsIntake.Tests.Application.Handlers;

public class GetClaimsQueryHandlerTests
{
    [Fact]
    public async Task FilterArguments_MapToRepositorySearchUnchanged()
    {
        var repository = new FakeClaimRepository();
        var handler = new GetClaimsQueryHandler(repository);
        var from = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        await handler.Handle(new GetClaimsQuery("underreview", "POL-1001", from, to, 3, 25), CancellationToken.None);

        Assert.NotNull(repository.LastSearch);
        var search = repository.LastSearch!.Value;
        Assert.Equal(ClaimStatus.UnderReview, search.Status); // case-insensitive parse
        Assert.Equal("POL-1001", search.PolicyNumber);
        Assert.Equal(from, search.FromUtc);
        Assert.Equal(to, search.ToUtc);
        Assert.Equal(3, search.PageNumber);
        Assert.Equal(25, search.PageSize);
    }

    [Fact]
    public async Task AbsentStatus_MapsToNullFilter()
    {
        var repository = new FakeClaimRepository();
        var handler = new GetClaimsQueryHandler(repository);

        await handler.Handle(new GetClaimsQuery(null, null, null, null), CancellationToken.None);

        Assert.Null(repository.LastSearch!.Value.Status);
        Assert.Equal(1, repository.LastSearch!.Value.PageNumber);
        Assert.Equal(20, repository.LastSearch!.Value.PageSize);
    }
}
