using Asp.Versioning;
using Asp.Versioning.Builder;
using ClaimsIntake.Application.Claims;
using ClaimsIntake.Application.Claims.Commands.SubmitClaim;
using ClaimsIntake.Application.Claims.Commands.UpdateClaimStatus;
using ClaimsIntake.Application.Claims.Queries.GetClaimById;
using ClaimsIntake.Application.Claims.Queries.GetClaims;
using MediatR;

namespace ClaimsIntake.API.Endpoints;

/// <summary>Body for the claim status PATCH endpoint.</summary>
public sealed record UpdateClaimStatusRequest(string Status, string? ReviewNotes);

public static class ClaimEndpoints
{
    /// <summary>
    /// Maps the versioned <c>/api/v{version}/claims</c> endpoint group with per-endpoint Swagger
    /// metadata. Must run before <c>DescribeApiVersions()</c> so the UI can discover the versions.
    /// </summary>
    public static IEndpointRouteBuilder MapClaimEndpoints(this IEndpointRouteBuilder app)
    {
        ApiVersionSet versionSet = app.NewApiVersionSet("Claims")
            .HasApiVersion(new ApiVersion(1, 0))
            .ReportApiVersions()
            .Build();

        var group = app.MapGroup("/api/v{version:apiVersion}/claims")
            .WithApiVersionSet(versionSet)
            .HasApiVersion(new ApiVersion(1, 0))
            .WithTags("Claims");

        group.MapPost("/", SubmitClaim)
            .WithName("SubmitClaim")
            .WithSummary("Submits a new claim.")
            .WithDescription("Creates a claim in the 'Submitted' status and returns it with a Location header.")
            .Produces<ClaimResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapGet("/", GetClaims)
            .WithName("GetClaims")
            .WithSummary("Lists claims, optionally filtered.")
            .WithDescription("Filters: status, policyNumber, submittedFrom, submittedTo (UTC) — all optional, combined with AND. Paged via pageNumber (default 1) and pageSize (default 20, max 100).")
            .Produces<IReadOnlyList<ClaimResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapGet("/{id:guid}", GetClaimById)
            .WithName("GetClaimById")
            .WithSummary("Gets a single claim by id.")
            .Produces<ClaimResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapPatch("/{id:guid}/status", UpdateClaimStatus)
            .WithName("UpdateClaimStatus")
            .WithSummary("Transitions a claim to a new status.")
            .WithDescription("Allowed transitions: Submitted→UnderReview; UnderReview→AdditionalInfoRequired/Approved/Denied; AdditionalInfoRequired→UnderReview. Illegal transitions return 409.")
            .Produces<ClaimResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }

    /// <summary>Submits a new claim. Validation runs in the MediatR pipeline; domain errors surface via the global exception handler.</summary>
    private static async Task<IResult> SubmitClaim(SubmitClaimCommand command, ISender sender, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var claim = await sender.Send(command, cancellationToken);

        var version = httpContext.RequestedApiVersion ?? new ApiVersion(1, 0);
        return Results.CreatedAtRoute("GetClaimById", new { id = claim.Id, version = version.ToString() }, claim);
    }

    /// <summary>Lists claims matching the optional query-string filters.</summary>
    private static async Task<IResult> GetClaims(
        string? status,
        string? policyNumber,
        DateTime? submittedFrom,
        DateTime? submittedTo,
        int? pageNumber,
        int? pageSize,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var claims = await sender.Send(
            new GetClaimsQuery(status, policyNumber, submittedFrom, submittedTo, pageNumber ?? 1, pageSize ?? 20),
            cancellationToken);
        return Results.Ok(claims);
    }

    /// <summary>Gets a single claim by id; 404 if it does not exist.</summary>
    private static async Task<IResult> GetClaimById(Guid id, ISender sender, CancellationToken cancellationToken)
    {
        var claim = await sender.Send(new GetClaimByIdQuery(id), cancellationToken);
        return Results.Ok(claim);
    }

    /// <summary>Transitions a claim through its lifecycle; illegal transitions return 409.</summary>
    private static async Task<IResult> UpdateClaimStatus(
        Guid id,
        UpdateClaimStatusRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var claim = await sender.Send(new UpdateClaimStatusCommand(id, request.Status, request.ReviewNotes), cancellationToken);
        return Results.Ok(claim);
    }
}
