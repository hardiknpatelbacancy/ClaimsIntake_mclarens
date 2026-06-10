using ClaimsIntake.Application.Claims.Commands.UpdateClaimStatus;
using ClaimsIntake.Application.Common.Exceptions;
using ClaimsIntake.Domain.Entities;
using ClaimsIntake.Domain.Enums;
using ClaimsIntake.Tests.Application.Fakes;

namespace ClaimsIntake.Tests.Application.Handlers;

public class UpdateClaimStatusCommandHandlerTests
{
    [Fact]
    public async Task UnknownClaimId_ThrowsNotFoundException()
    {
        var handler = new UpdateClaimStatusCommandHandler(new FakeClaimRepository());
        var command = new UpdateClaimStatusCommand(Guid.NewGuid(), "UnderReview", null);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task LegalTransition_PersistsNewStatus()
    {
        var repository = new FakeClaimRepository();
        var claim = Claim.Submit("POL-1001", "Jane Doe", "desc", DateOnly.FromDateTime(DateTime.UtcNow));
        repository.Claims.Add(claim);
        var handler = new UpdateClaimStatusCommandHandler(repository);

        var response = await handler.Handle(
            new UpdateClaimStatusCommand(claim.Id, "UnderReview", "Assigned."), CancellationToken.None);

        Assert.Equal(nameof(ClaimStatus.UnderReview), response.Status);
        Assert.Equal(ClaimStatus.UnderReview, claim.Status);
        Assert.Equal(1, repository.SaveChangesCalls);
    }
}
