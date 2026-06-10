# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```powershell
# Build (solution file is .slnx, not .sln)
dotnet build ClaimsIntake.slnx

# Run the API (Development; applies pending migrations on startup, serves Swagger UI at /swagger,
# health check incl. DB connectivity at /health)
dotnet run --project src\ClaimsIntake.API

# Tests (xUnit)
dotnet test ClaimsIntake.slnx
dotnet test --filter "FullyQualifiedName~ClaimTests"   # single class
dotnet test --filter "DisplayName~transition"          # by name fragment

# EF Core migrations (requires the dotnet-ef global tool; migrations live in
# src\ClaimsIntake.Infrastructure\Persistence\Migrations)
dotnet ef migrations add <Name> --project src\ClaimsIntake.Infrastructure --startup-project src\ClaimsIntake.API --output-dir Persistence\Migrations
dotnet ef database update --project src\ClaimsIntake.Infrastructure --startup-project src\ClaimsIntake.API
```

Target framework is **net10.0**. The database is SQL Server, configured via the `ClaimsIntake` connection string in `appsettings.json` (there is no Development override — point it at any reachable instance; the API creates and migrates the database on startup in Development). Don't commit real credentials in that file.

## Architecture

Clean Architecture, four projects under `src/`, dependencies point inward only:

- **Domain** → nothing (zero NuGet references — keep it that way)
- **Application** → Domain (MediatR, FluentValidation)
- **Infrastructure** → Application + Domain (EF Core, SQL Server)
- **API** → composition root (Minimal APIs for v1, attribute-routed controllers for v2, Asp.Versioning, Swashbuckle, Serilog)

`ARCHITECTURE.md` is the original design document, but the implementation deliberately diverges from it in two places (explicit user decisions — don't "fix" the code to match the doc):

- §2 rejects MediatR, but the code **uses MediatR 12.5.0** with a `ValidationBehaviour` pipeline. The 12.x line is pinned for its Apache license; don't bump to 13.x (commercial license).
- §4 rejects a generic repository, but the code has **both** `IRepository<T>` and `IClaimRepository` (the latter extends the former). Both interfaces live in `Application\Abstractions`; `ClaimRepository` in Infrastructure implements both and is registered so both resolve to the same scoped instance.

The doc also describes a richer domain model (ClaimNumber, RowVersion, status history, Claimant/Policy entities) than what is implemented — only the `Claim` aggregate exists.

`README.md` documents the design rationale and a list of *deliberate* limitations (no optimistic concurrency token, `ReviewNotes` overwritten on each transition rather than kept as history, no auth, `DateTime.UtcNow` directly instead of `TimeProvider`). These are known and accepted — don't "fix" them unless asked.

### Request flow and error contract

Endpoint (thin, in `API\Endpoints\ClaimEndpoints.cs`) → `ISender.Send(command/query)` → `ValidationBehaviour<,>` runs all FluentValidation validators → handler in Application → repository / domain entity → response DTO (`ClaimResponse`).

**Never call `.Validate()` manually in endpoints or handlers.** Validation failures, domain rule violations, and missing resources are all thrown as exceptions and converted to RFC 7807 responses in exactly one place, `API\Middleware\GlobalExceptionHandler.cs`:

| Exception | Status |
|---|---|
| `FluentValidation.ValidationException` | 422 (RFC 4918 type, `errors` dict keyed by camelCase field) |
| `DomainException` (e.g. `InvalidClaimStateTransitionException`) | 409 |
| `NotFoundException` (Application layer) | 404 |
| anything else | 500 (detail suppressed in Production, logged) |

The 422/409 split is intentional: an unparseable status *value* is a validation error (422, validator); a parseable value that's an illegal *transition* is a domain decision (409, entity).

### Domain model

`Claim` is a rich (non-anemic) aggregate: private setters, a private parameterless constructor reserved for EF, the `Claim.Submit(...)` factory, and `TransitionTo(status, notes)` which enforces the state machine encoded in the entity's `AllowedTransitions` map:

```
Submitted → UnderReview → AdditionalInfoRequired (→ back to UnderReview)
                        → Approved | Denied   (terminal)
```

Business rules enforced by validators (not the entity): `PolicyNumber` must match `POL-` + 4–10 digits; `IncidentDate` must be within the past 2 years and not in the future; `ReviewNotes` is mandatory when transitioning to `Denied` or `AdditionalInfoRequired`. Status strings in requests are parsed case-insensitively.

EF configuration is annotation-free, via `IEntityTypeConfiguration<>` classes in `Infrastructure\Persistence\Configurations` (auto-applied by assembly scan). `ClaimStatus` is persisted as a string.

### API versioning — never hardcode "v1"

Versions are driven entirely by `Asp.Versioning`; the literal string `"v1"` must not appear in routes, Swagger config, or generated URLs:

- Routes use the template `/api/v{version:apiVersion}/claims`. The v1 minimal-API endpoint group declares its versions via `.WithApiVersionSet(...)` and `.HasApiVersion(new ApiVersion(1, 0))`; the v2 controller (`API\Controllers\ClaimsController.cs`) declares its version with `[ApiVersion(2.0)]` (controller versioning support comes from `.AddMvc()` on the versioning builder in `Program.cs`).
- Swagger documents are generated per *discovered* version: `ConfigureSwaggerGenOptions` (in `API\Swagger`) loops over `IApiVersionDescriptionProvider.ApiVersionDescriptions`, and the Swagger UI loops over `app.DescribeApiVersions()` — which only works because it runs **after** `MapClaimEndpoints()` and `MapControllers()` in `Program.cs`; keep that ordering.
- Group names come from `AddApiExplorer` (`GroupNameFormat = "'v'VVV"`, `SubstituteApiVersionInUrl = true`).
- `Location` headers are built with `Results.CreatedAtRoute(...)` passing the requested version via the `httpContext.RequestedApiVersion` extension property, not a formatted URL string. (In Asp.Versioning 10 the old `GetRequestedApiVersion()` method no longer exists — it became C# extension properties on `HttpContext`.)

v2 exists and is controller-based, exposing only `GET /api/v2/claims`; it reuses the same `GetClaimsQuery` pipeline as v1. To add another version: declare it on a (new) endpoint group via `.HasApiVersion(...)` or on a controller via `[ApiVersion(...)]` — Swagger and the UI dropdown pick it up automatically with zero config changes.

### Tests

`tests\ClaimsIntake.Tests` — xUnit, four suites: domain state-machine matrix (`Domain\ClaimTests`), validator rules (FluentValidation.TestHelper), handler units (hand-written `FakeClaimRepository`, no mocking library), and HTTP integration (`Integration\`). Integration tests run the real pipeline via `WebApplicationFactory<Program>` — enabled by the `public partial class Program` line at the bottom of `Program.cs`; don't remove it. The test host uses environment `"Testing"` (so the Development-only startup migration is skipped), swaps the DbContext to SQLite in-memory in `ClaimsApiFactory`, and creates schema with `EnsureCreated()` because the checked-in migrations are SQL Server dialect. Note: 422 `errors` keys are camelCase (`policyNumber`), and POST's `Location` header is an absolute URL.

### Adding a use case

1. Command/query record + validator + handler under `Application\Claims\Commands|Queries\<UseCase>\` — registration is automatic (assembly scanning in `ApplicationRegistration`).
2. Map the endpoint in `ClaimEndpoints` with `.Produces*` metadata for every status code it can return (Swagger contract is maintained by hand).
3. New repository needs go on `IClaimRepository` (domain-specific names, not generic predicates), implemented in `ClaimRepository`.
