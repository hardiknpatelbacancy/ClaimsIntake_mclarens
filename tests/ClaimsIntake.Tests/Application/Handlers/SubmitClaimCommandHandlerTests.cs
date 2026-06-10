using ClaimsIntake.Application.Claims.Commands.SubmitClaim;
using ClaimsIntake.Domain.Enums;
using ClaimsIntake.Tests.Application.Fakes;

namespace ClaimsIntake.Tests.Application.Handlers;

public class SubmitClaimCommandHandlerTests
{
    [Fact]
    public async Task HappyPath_PersistsClaimAndReturnsResponse()
    {
        var repository = new FakeClaimRepository();
        var handler = new SubmitClaimCommandHandler(repository);
        var command = new SubmitClaimCommand(
            "POL-1001", "Jane Doe", "Rear-end collision", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7)));

        var response = await handler.Handle(command, CancellationToken.None);

        var stored = Assert.Single(repository.Claims);
        Assert.Equal(1, repository.SaveChangesCalls);
        Assert.Equal(stored.Id, response.Id);
        Assert.Equal("POL-1001", response.PolicyNumber);
        Assert.Equal(ClaimStatus.Submitted, stored.Status);
        Assert.Equal(nameof(ClaimStatus.Submitted), response.Status);
    }
}
