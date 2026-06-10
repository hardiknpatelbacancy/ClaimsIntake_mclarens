using Asp.Versioning;
using ClaimsIntake.Application.Claims;
using ClaimsIntake.Application.Claims.Queries.GetClaims;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ClaimsIntake.API.Controllers;

/// <summary>
/// Version 2 of the claims API, controller-based. Exposes only the list endpoint;
/// every other operation remains v1-only in <see cref="Endpoints.ClaimEndpoints"/>.
/// The version is declared via the attribute — the route template and Swagger
/// discovery pick it up automatically, so no version literal appears anywhere else.
/// </summary>
[ApiController]
[ApiVersion(2.0)]
[Route("api/v{version:apiVersion}/claims")]
[Tags("Claims")]
public sealed class ClaimsController : ControllerBase
{
    private readonly ISender _sender;

    /// <summary>Creates the controller with the MediatR sender used to dispatch queries.</summary>
    public ClaimsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>Lists claims, optionally filtered.</summary>
    /// <remarks>
    /// Filters: status, policyNumber, submittedFrom, submittedTo (UTC) — all optional,
    /// combined with AND. Paged via pageNumber (default 1) and pageSize (default 20, max 100).
    /// </remarks>
    [HttpGet(Name = "GetClaimsV2")]
    [ProducesResponseType<IReadOnlyList<ClaimResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IReadOnlyList<ClaimResponse>>> GetClaims(
        [FromQuery] string? status,
        [FromQuery] string? policyNumber,
        [FromQuery] DateTime? submittedFrom,
        [FromQuery] DateTime? submittedTo,
        [FromQuery] int? pageNumber,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var claims = await _sender.Send(
            new GetClaimsQuery(status, policyNumber, submittedFrom, submittedTo, pageNumber ?? 1, pageSize ?? 20),
            cancellationToken);
        return Ok(claims);
    }
}
