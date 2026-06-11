# ClaimsIntake API

A claims intake service for an insurance domain: submit a claim, query claims, and move them through a controlled lifecycle. Built with **.NET 10**, Minimal APIs, EF Core (SQL Server), MediatR, and FluentValidation, structured as Clean Architecture.

`ARCHITECTURE.md` contains the original design document; this README describes what was actually built, the reasoning behind it, and the trade-offs taken.

## Getting started

Prerequisites: .NET 10 SDK and a SQL Server instance (LocalDB, Express, or full).

1. Set the `ClaimsIntake` connection string in `src/ClaimsIntake.API/appsettings.json` to point at your SQL Server (an `appsettings.Development.json` override or environment variable also works; never commit real credentials).
2. Run:

   ```powershell
   dotnet run --project src\ClaimsIntake.API
   ```

   In Development the API applies pending EF Core migrations on startup (creating the database if needed), so no manual `dotnet ef database update` is required.

3. Open **http://localhost:5157/swagger** for the Swagger UI, or **/health** for the health check (includes a database connectivity probe).

Tests: `dotnet test ClaimsIntake.slnx` — domain state-machine matrix, validator rules, handler units (hand-written fakes), and full-pipeline HTTP integration tests (`WebApplicationFactory` over SQLite in-memory; no SQL Server needed).

## API surface

Two API versions are live, deliberately implemented in both endpoint styles:

- **v1** (`/api/v1/...`) — the full surface, implemented as **Minimal APIs** (`API\Endpoints\ClaimEndpoints.cs`).
- **v2** (`/api/v2/...`) — **controller-based** (`API\Controllers\ClaimsController.cs`), currently exposing only the claims list endpoint.

Both styles plug into the same `Asp.Versioning` machinery (an `ApiVersionSet` on the minimal endpoint group, an `[ApiVersion]` attribute on the controller), run the same MediatR pipeline and validators, and share the same RFC 7807 error contract — the Swagger UI shows one document per version with zero version-specific configuration.

| Method | Route | Purpose | Success | Errors |
|---|---|---|---|---|
| POST | `/api/v1/claims` | Submit a new claim | 201 + `Location` | 400, 422 |
| GET | `/api/v1/claims` | List claims (filters: `status`, `policyNumber`, `submittedFrom`, `submittedTo`; paged via `pageNumber`/`pageSize`, default 20, max 100) | 200 | 400, 422 |
| GET | `/api/v1/claims/{id}` | Get a claim by id | 200 | 404 |
| PATCH | `/api/v1/claims/{id}/status` | Transition a claim's status | 200 | 400, 404, 409, 422 |
| GET | `/api/v2/claims` | List claims (controller-based; same filters and paging as v1) | 200 | 400, 422 |

Claim lifecycle:

```
Submitted → UnderReview → Approved | Denied        (terminal)
                        → AdditionalInfoRequired → UnderReview
```

Validation rules worth knowing: `policyNumber` must match `POL-` + 4–10 digits; `incidentDate` must be within the past 2 years (inclusive of today); transitions to `Denied` or `AdditionalInfoRequired` require `reviewNotes`.

All errors are RFC 7807 `application/problem+json`. Validation failures return **422** with an `errors` dictionary keyed by camelCase field name; illegal state transitions return **409** with the domain's explanation; unknown ids return **404**; unexpected errors return **500** with detail suppressed outside Development.

## Design decisions

**Clean Architecture, four projects.** `Domain` (no NuGet references at all) ← `Application` (use cases, validation, repository abstractions) ← `Infrastructure` (EF Core, repositories) ← `API` (composition root). The compiler enforces the dependency rule; persistence and HTTP concerns cannot leak into the domain.

**Rich domain model, not an anemic one.** `Claim` has private setters, a `Claim.Submit(...)` factory, and a `TransitionTo(...)` method backed by an explicit allowed-transitions map. The state machine lives in the entity, so an invalid transition is impossible to persist regardless of which handler or future endpoint touches the claim. This is the most heavily protected invariant in the system.

**Two kinds of "invalid status", two status codes — deliberately.** An unparseable status value (`"Banana"`) is a *validation* error: the request is malformed, FluentValidation catches it, the client gets 422. A parseable value that is an illegal *transition* (`Submitted → Approved`) is a *domain* decision: the request was well-formed but conflicts with the resource's current state, the entity throws, the client gets 409. Collapsing these into one code loses information clients can act on.

**One validation chokepoint, one error-mapping chokepoint.** Validators run automatically in a MediatR `ValidationBehaviour` pipeline before any handler executes — there is not a single manual `.Validate()` call in the codebase. All exception-to-HTTP mapping happens in exactly one place (`GlobalExceptionHandler`). Endpoints stay three lines long: bind → send → shape the success response.

**CQRS-light.** Commands and queries are separate request/handler classes with their own models, but there is one database and one schema. The in-code separation costs almost nothing and keeps write paths going through the aggregate while read paths use `AsNoTracking()` projections; separate read stores would be unjustified at this scale.

**Specific + generic repository.** `IClaimRepository` (domain-specific query methods like `SearchAsync`, `GetByStatusAsync`) extends a generic `IRepository<T>`; one implementation serves both, registered so both interfaces resolve to the same scoped instance. The generic interface was an explicit requirement; the specific one is where new query needs should land — with named, intention-revealing methods rather than predicate escape hatches that leak EF semantics.

**EF Core code-first, persistence-ignorant domain.** Mapping lives in `IEntityTypeConfiguration<>` classes (no attributes on the entity), `ClaimStatus` is persisted as a string (readable in queries, safe against enum reordering), and indexes on `PolicyNumber`, `Status`, and `SubmittedAt` match the list endpoint's filters.

**Versioning machinery from day one — with no hardcoded versions.** URL-segment versioning via `Asp.Versioning`. Routes use the `/api/v{version:apiVersion}/...` template; minimal-API endpoint groups declare their versions through an `ApiVersionSet`, attribute-routed controllers declare theirs with `[ApiVersion]`, and Swagger documents (and the UI dropdown) are generated per *discovered* version from `IApiVersionDescriptionProvider` — no version literal appears in routes or Swagger configuration. v2 (the controller-based `ClaimsController`) was added exactly this way: declare the version on the controller, `MapControllers()` in `Program.cs`, and the v2 Swagger document appeared with no other changes. A future v3 works the same in either style.

**Structured logging with Serilog**, configured entirely from `appsettings.json`, with request logging and EF command logging surfaced in Development.

**Where the implementation diverges from ARCHITECTURE.md.** The design doc argued against MediatR (§2) and against a generic repository (§4); both are present here as explicit requirements of the exercise. MediatR is pinned to the 12.x line, the last Apache-licensed release — do not bump to 13.x without a license decision. The doc also sketches a richer model (claim numbers, status history, Claimant/Policy entities) that was descoped to a single `Claim` aggregate.

## Why MediatR

MediatR was chosen not for complexity but specifically for the ValidationBehaviour pipeline, which provides a single consistent validation execution point across all commands and queries without per-endpoint wiring.

Given the exercise signals future microservices growth, the CQRS separation it enables has a credible upgrade path.

For a truly isolated API with no growth signals, a plain IClaimService would have been the simpler and equally valid choice.

## Assumptions

- **One aggregate.** `PolicyNumber` and `ClaimantName` are plain strings on the claim; there is no Policy or Claimant entity and no verification that a policy exists or was in force on the incident date.
- **`ReviewNotes` is current-state, not history.** A transition with notes overwrites the previous notes. An append-only status history table is the obvious next step (see below).
- **Timestamps are server-clock UTC** (`DateTime.UtcNow` inside the entity); the incident date must not be in the future and must be within the past 2 years (a filing-window assumption — real policies would drive this from product rules).
- **Policy numbers follow `POL-` + 4–10 digits** — a stand-in format giving validation something concrete to check, since no policy system exists to verify against.
- **`AdditionalInfoRequired` returns to `UnderReview`** when info arrives, and `Approved`/`Denied` are terminal with no appeal path — the spec's arrow diagram left both ends ambiguous, so I encoded the most conservative reading.
- **No authentication or authorization** — anyone can transition any claim. The endpoint groups are the one-line seam for `.RequireAuthorization()` when an identity provider is wired in.
- **Single-instance writes.** There is no optimistic concurrency token, so two simultaneous transitions of the same claim race (last write wins).
- **Migrations on startup are a Development convenience only**, guarded by an environment check; production schema changes belong in a deployment pipeline.
