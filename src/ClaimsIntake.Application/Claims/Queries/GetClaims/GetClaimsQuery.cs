using MediatR;

namespace ClaimsIntake.Application.Claims.Queries.GetClaims;

/// <summary>All filters are optional and combine with AND semantics. Results are paged.</summary>
public sealed record GetClaimsQuery(
    string? Status,
    string? PolicyNumber,
    DateTime? SubmittedFrom,
    DateTime? SubmittedTo,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<IReadOnlyList<ClaimResponse>>;
