# ClaimsIntakeAPI — Architecture Decision Document

**Scope:** A claims intake service for an insurance domain: register a claim, attach supporting metadata, move it through a controlled lifecycle, and expose query endpoints for operators. Built as an interview exercise, but structured so every decision would survive contact with a real production roadmap.

**Guiding principle:** Demonstrate judgment, not pattern bingo. Every layer and pattern below earns its place; everything that doesn't is explicitly cut in §6.

> **Implementation status (June 2026).** This document is the original design record; the implemented code deliberately diverges from it in five places (see `README.md` for current rationale and `CLAUDE.md` for working conventions):
>
> 1. **MediatR is used** (contra §2), as an explicit exercise requirement — its `ValidationBehaviour` pipeline is the single validation execution point. Pinned to the Apache-licensed 12.x line; do not upgrade to 13.x (commercial license).
> 2. **A generic `IRepository<T>` exists alongside `IClaimRepository`** (contra §4), also an explicit requirement; both resolve to the same scoped `ClaimRepository`.
> 3. **The domain model is a single `Claim` aggregate** — no `Claimant`/`Policy`/`ClaimDocument`/`ClaimStatusHistory` entities, no `ClaimNumber`, no `RowVersion` yet (§3 describes the aspirational model).
> 4. **The lifecycle differs from §3's enum:** implemented as `Submitted → UnderReview → AdditionalInfoRequired (→ UnderReview) | Approved | Denied`, with no Draft, Closed, or appeal transition.
> 5. **Validation failures map to HTTP 422** (not the 400 stated in §5), distinguishing malformed input (422) from illegal state transitions (409).

---

## 1. Solution Structure

Classic Clean Architecture, four projects plus tests. Dependencies point inward only.

```
ClaimsIntakeAPI.sln
│
├── src/
│   ├── ClaimsIntake.Domain            (Core — no dependencies)
│   ├── ClaimsIntake.Application       (Core — depends on Domain)
│   ├── ClaimsIntake.Infrastructure    (Adapter — depends on Application, Domain)
│   └── ClaimsIntake.Api               (Host — composition root, depends on all)
│
└── tests/
    ├── ClaimsIntake.UnitTests         (Domain + Application, no I/O)
    └── ClaimsIntake.IntegrationTests  (API-level, WebApplicationFactory + real EF pipeline)
```

| Project | Layer | Responsibility |
|---|---|---|
| **ClaimsIntake.Domain** | Domain | Entities, enums, domain invariants (state-transition rules live *here*, not in services), domain exceptions. Zero NuGet references — not even EF. |
| **ClaimsIntake.Application** | Application | Use cases (commands/queries as plain handler classes), request/response DTOs, validation (FluentValidation), abstractions the infrastructure must implement (`IClaimRepository`, `IUnitOfWork`, `IClaimsDbReader`). |
| **ClaimsIntake.Infrastructure** | Infrastructure | EF Core `DbContext`, entity configurations (`IEntityTypeConfiguration<>`), migrations, repository implementations, SQL Server specifics. |
| **ClaimsIntake.Api** | Presentation/Host | Minimal API endpoint definitions, API versioning, ProblemDetails error mapping, DI wiring, Swagger. Endpoints are thin: bind → call handler → map result to HTTP. |
| **ClaimsIntake.UnitTests** | Tests | Domain invariants (especially the claim state machine) and application handlers with faked abstractions. The state machine tests are the highest-value tests in the solution. |
| **ClaimsIntake.IntegrationTests** | Tests | Full HTTP slice via `WebApplicationFactory`, EF against SQL (LocalDB or Testcontainers). Verifies routing, validation, persistence, and versioning actually compose. |

**Why four projects and not two?** With two (Api + everything), the domain model inevitably leaks EF and HTTP concerns within a sprint or two. With six-plus, you're paying assembly tax for an intake API. Four is the long-standing sweet spot: the compiler enforces the dependency rule, and each project has exactly one reason to change.

---

## 2. CQRS: A Deliberate "Light Yes, Heavy No"

**Decision: separate command and query *handlers* in the Application layer. No separate read/write databases, no event sourcing, no MediatR.**

Being explicit about what "CQRS" means here, because the term covers three very different commitments:

1. **Separate read and write *models in code*** (different classes for "create a claim" vs "list claims") — **adopted.** This costs almost nothing and pays immediately: write paths go through the aggregate and its invariants; read paths project straight from EF queries into DTOs with `AsNoTracking()`, skipping the repository entirely. Claims intake is naturally read-heavy (operators search/filter constantly, claims are written once and transitioned a handful of times), so the query side deserves its own shape — flattened list projections, paging — that the domain model shouldn't be contorted to serve.

2. **Separate read and write *stores*** (replicated read DB, eventual consistency) — **rejected.** This is justified by read/write loads that scale at different orders of magnitude or by reporting needs that can't be served by indexed views. An intake API at this stage has neither. Taking on replication lag, projection rebuilds, and dual-store ops for an interview-scale system is résumé-driven architecture.

3. **MediatR as the dispatch mechanism** — **rejected.** MediatR adds an indirection layer whose main benefits (pipeline behaviors for logging/validation) are already covered by endpoint filters and FluentValidation in this design. With Minimal APIs, injecting a concrete handler into the endpoint delegate is one line and fully navigable in the IDE ("go to definition" works; with MediatR it doesn't). If cross-cutting pipelines genuinely multiply later, introducing a mediator is a mechanical refactor — the handler classes already exist with the right shape.

**The migration path is the point:** because commands and queries are already separate classes with separate models, scaling up to option 2 later (e.g., a Dapper read side, then a replica) changes only Infrastructure. The expensive decision is deferred without closing the door.

---

## 3. Database: SQL Server + EF Core Code-First

SQL Server because claims data is relational and transactional to its core — a claim and its status history must commit atomically, and operators query by combinations of status/date/policy that indexes serve well. Code-first with migrations because the domain model is the source of truth and schema evolution stays in source control and CI.

### Domain Model

```
Policy (1) ──── (N) Claim (1) ──── (N) ClaimStatusHistory
                       │
                       └─── (N) ClaimDocument
Claimant (1) ── (N) Claim
```

**Entities**

- **Claim** — the aggregate root. `Id (Guid)`, `ClaimNumber` (human-readable, unique, generated on submit), `PolicyId`, `ClaimantId`, `ClaimType`, `Status`, `IncidentDate`, `ReportedDate`, `Description`, `EstimatedAmount (decimal(18,2))`, `RowVersion` (optimistic concurrency — two adjusters acting on the same claim is the realistic conflict in this domain). All state changes go through methods (`Submit()`, `Approve()`, `Reject(reason)`), never property setters; each transition validates against the state machine and appends a history entry.
- **Claimant** — `Id`, name, contact details. Separate entity (not owned) because one claimant files multiple claims and dedup matters in real claims systems.
- **Policy** — deliberately thin: `Id`, `PolicyNumber`, `EffectiveFrom/To`. In production this is another system's data; here it exists so claim validation ("incident date within policy period") has something real to check. Treated as reference data — this API never mutates it.
- **ClaimDocument** — metadata only (`FileName`, `ContentType`, `SizeBytes`, `UploadedAt`). Blob storage itself is out of scope (§6); the entity proves the model accounts for it.
- **ClaimStatusHistory** — append-only: `ClaimId`, `FromStatus`, `ToStatus`, `Reason`, `ChangedAt`, `ChangedBy (string placeholder until auth exists)`. Written exclusively by the aggregate's transition methods, giving an audit trail for free.

**Enums**

```
ClaimStatus:  Draft → Submitted → UnderReview → Approved | Rejected → Closed
              (Rejected → UnderReview allowed once, for appeal; everything else invalid)
ClaimType:    Auto | Property | Liability | WorkersComp
```

Lifecycle transitions are encoded as an explicit allowed-transitions map inside `Claim` — an invalid transition throws a domain exception that the API maps to HTTP 409. This state machine is the heart of the domain and the first thing unit-tested.

**Mapping choices:** enums persisted as strings (readable in queries, safe against reordering); `IEntityTypeConfiguration<>` classes per entity rather than annotations (keeps Domain persistence-ignorant); indexes on `Claim.Status`, `Claim.ClaimNumber (unique)`, and `(PolicyId, Status)` to match the query side's access patterns.

---

## 4. Repository Pattern: Specific Over Generic

**Decision: one specific repository per aggregate root (`IClaimRepository`), defined in Application, implemented in Infrastructure. No generic `IRepository<T>`. Queries bypass repositories entirely.**

The trade-off, honestly stated:

- **Generic repository** (`IRepository<T>` with `GetById/Add/Remove/Find(Expression<Func<T,bool>>)`) promises code reuse, but over EF Core it's an abstraction over an abstraction — `DbContext`/`DbSet` *already are* a unit of work and generic repositories. Worse, the `Find(predicate)` escape hatch leaks `IQueryable` semantics through the interface, so callers still couple to EF query translation quirks while pretending they haven't. You get the ceremony of decoupling without the decoupling. Twenty years in, this is the pattern I've removed from more codebases than I've added it to.
- **Specific repository** costs a few more lines per aggregate but gives an interface that speaks the domain's language: `GetByIdWithHistoryAsync`, `GetByClaimNumberAsync`, `Add`. It defines *exactly* which load shapes exist (no accidental lazy-loading or unbounded includes), it's trivially fakeable in unit tests without an EF in-memory provider, and it guards the invariant that writes load the full aggregate.

With only one true aggregate root (Claim — Claimant and Policy are looked up, not orchestrated), the generic pattern's reuse argument evaporates anyway: there's nothing to reuse across.

**Where repositories deliberately do *not* apply:** the query side (§2). List/search endpoints inject a thin `IClaimsDbReader` (or the `DbContext` directly via the Application-layer abstraction) and project to DTOs with `AsNoTracking()`. Forcing reads through a repository produces either a bloated interface (`GetPagedFilteredSortedAsync(...)` with ten parameters) or N nearly identical methods. Repositories protect write-side invariants; reads have no invariants to protect.

`IUnitOfWork.SaveChangesAsync()` is exposed as its own abstraction so handlers — not repositories — own transaction boundaries.

---

## 5. API Style: Minimal API with URL-Segment Versioning

**Minimal APIs**, organized as one endpoint-group class per resource (`ClaimEndpoints.MapClaimEndpoints(RouteGroupBuilder)`), not controllers. Rationale: for a focused service the endpoint-per-use-case model maps 1:1 onto the command/query handlers from §2, avoids controller base-class baggage, and is where the platform's investment is going (.NET 8+ AOT, validation, OpenAPI improvements all land on Minimal APIs first). The known historical objection — "Minimal APIs become a 500-line Program.cs" — is an organization failure, not a framework one; route groups solve it.

**Versioning: URL segment (`/api/v1/claims`) via `Asp.Versioning.Http`.**

- URL-segment over header/query-string versioning because it's explicit, cacheable, trivially testable with curl, and unambiguous in logs and support tickets. Header versioning is academically purer (the resource URI doesn't change) but every team I've seen run it pays a discoverability tax forever.
- `Asp.Versioning` (the maintained successor to `Microsoft.AspNetCore.Mvc.Versioning`) with `ApiVersionSet` on the route groups, reporting supported versions in response headers.
- Policy: v1 only ships now. The point of installing the machinery on day one is that adding v2 later is additive — no retrofit of every route when the first breaking change arrives.

Cross-cutting API decisions: `ProblemDetails` (RFC 7807) for all errors including domain-exception → 409 and validation → 400 mappings; FluentValidation executed via an endpoint filter; Swagger/OpenAPI per version.

---

## 6. Deliberately Skipped — and Why

Listed explicitly because knowing what *not* to build is the actual senior skill being assessed.

| Skipped | Why, specifically |
|---|---|
| **AuthN/AuthZ** | Real auth is an identity-provider integration (Entra ID/Auth0), which is configuration, not architecture — and fake auth is worse than none because it implies security that isn't there. The seam is prepared: `ChangedBy` on the audit trail, and endpoint groups where `.RequireAuthorization()` is a one-line addition. |
| **Messaging / outbox / events** | An intake API has no second consumer yet. Publishing `ClaimSubmitted` events to a bus nobody listens to is speculative generality, and a correct outbox (idempotency, poison handling, ordering) would dominate the exercise. The aggregate's transition methods are the single choke point where domain-event raising plugs in later — the seam exists, the infrastructure doesn't. |
| **Saga / process manager** | Sagas coordinate multi-service transactions. This is one service with one database; EF's transaction *is* the consistency mechanism. A saga here is a solution shipped before its problem. |
| **Separate read store / heavy CQRS** | Argued in §2 — the in-code separation is kept, the operational cost is deferred. |
| **Caching (Redis/output cache)** | Cache invalidation on a stateful lifecycle entity is a correctness risk taken to solve a performance problem we don't have. Indexes (§3) cover this scale. |
| **Actual file/blob storage** | `ClaimDocument` stores metadata only. Streaming uploads, virus scanning, and SAS tokens are an infrastructure project of their own; the domain model already accounts for the data. |
| **Multi-tenancy** | Pervasive (query filters, tenant resolution, test matrix) and absent from requirements. Retrofitting is genuinely painful, which is exactly why it must be a deliberate product decision, not an architect's guess. |
| **Microservices / modular monolith ceremony** | One bounded context, one deployable. The Clean Architecture seams are the extraction points if a split is ever warranted by team or scale — not before. |
| **Kubernetes/Docker orchestration, resilience policies (Polly), health-check dashboards** | Deployment topology is the client's call. A basic `/health` endpoint ships; the rest is platform engineering, not solution architecture. |
| **AutoMapper** | Hand-written mapping in handlers: claim DTOs are small, the mapping *is* logic worth seeing in review, and runtime-reflection mapping failures are a classic production foot-gun that saves ~20 lines here. |

**What ships despite the cuts:** optimistic concurrency, a tested state machine, an append-only audit trail, versioning machinery, ProblemDetails, and integration tests through the real pipeline — the things that are expensive to retrofit and cheap to include now.
