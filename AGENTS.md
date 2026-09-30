# AGENTS.md — ResolveOps Coding Agent Guide

> **Single source of truth for all coding agents and contributors.**
> Read this file before changing any code. Then read the master specification.

---

## Master Specification

```
c:\Personal\ResolveOps\LOGISTICS_EXCEPTION_CARRIER_CLAIMS_MASTER_SPEC.md
```

The spec is the authoritative reference. This file summarizes its key operating rules
and adds current phase status. When in conflict, the spec wins.

---

## Current Phase

| Field | Value |
|---|---|
| **Current phase** | Phase 18 — AI assistance & RAG (Version 3 only) |
| **Phase status** | ✅ Complete |
| **Next phase** | Phase 17 — CI/CD and deployment |
| **Last updated** | 2026-09-30 |

### Phase 0 deliverables completed

- [x] Repository structure matches `docs/diagrams/`
- [x] `global.json` pins .NET SDK 10.0.302
- [x] `Directory.Build.props` — nullable, warnaserror, analyzers, deterministic
- [x] `Directory.Packages.props` — central package management
- [x] Solution file `ResolveOps.slnx` with all empty project stubs
- [x] `AGENTS.md`, `CHANGELOG.md`, `README.md`
- [x] `docs/glossary.md`, `docs/assumptions.md`, `docs/test-strategy.md`
- [x] ADR-001 through ADR-006, ADR-021, ADR-022
- [x] All 5 architecture diagrams (Mermaid)
- [x] `dotnet build` passes with 0 errors, 0 warnings
- [x] No business feature code

---

## Build and Run Commands

```bash
# Restore all packages
dotnet restore ResolveOps.slnx

# Build entire solution (warnings treated as errors)
dotnet build ResolveOps.slnx --configuration Release

# Format check (no changes allowed in CI)
dotnet format ResolveOps.slnx --verify-no-changes

# Apply formatting
dotnet format ResolveOps.slnx

# Run unit tests
dotnet test tests/ResolveOps.UnitTests/ --configuration Release

# Run architecture tests
dotnet test tests/ResolveOps.ArchitectureTests/ --configuration Release

# Run integration tests (requires Docker for Testcontainers)
dotnet test tests/ResolveOps.IntegrationTests/ --configuration Release

# Run all tests
dotnet test ResolveOps.slnx --configuration Release

# EF Core migrations (Phase 1+)
dotnet ef migrations add <MigrationName> --project src/BuildingBlocks/ResolveOps.Persistence --startup-project src/Hosts/ResolveOps.Api

# Apply migrations
dotnet ef database update --project src/BuildingBlocks/ResolveOps.Persistence --startup-project src/Hosts/ResolveOps.Api
```

### Phase 1+ (once Aspire AppHost is configured)

```bash
# Start full local stack (SQL Server, RabbitMQ, Redis, MinIO, Seq, Mailpit)
dotnet run --project src/AppHost/ResolveOps.AppHost

# Alternatively via Docker Compose (Phase 17+)
docker compose -f deploy/docker/docker-compose.yml up
```

---

## Architecture Constraints

1. **Modular Monolith** — one deployable API + one Worker process. No microservices.
2. **Vertical Slice Architecture** — each feature owns its endpoint, validation, handler, mapping, and tests. See `docs/diagrams/module-diagram.md`.
3. **No MediatR** — feature handlers are resolved directly via DI.
4. **No AutoMapper** — all mapping is explicit, written by hand.
5. **No generic repository** — EF Core `DbContext` is used directly in handlers.
6. **No Azure proprietary services** — open-source stack only (see ADR-022).
7. **Domain logic belongs in domain/application layer** — not in endpoints or EF queries.
8. **Tenant isolation is mandatory** — EF query filters + authorization handlers + tenant-scoped caches and blob paths.
9. **Outbox pattern** — business state and integration events commit in one SQL Server transaction.
10. **Inbox idempotency** — every message consumer inserts an inbox record before executing side effects.

---

## Naming Rules

| Concern | Convention |
|---|---|
| Projects | `ResolveOps.<Layer>` or `ResolveOps.Modules.<Name>` |
| Namespaces | Match project and folder hierarchy |
| Feature folders | `Features/<FeatureName>/` inside module `Application/` |
| Endpoint files | `<Action><Resource>Endpoint.cs` |
| Handler files | `<Action><Resource>Handler.cs` |
| Command/query | `<Action><Resource>Command.cs` / `<Action><Resource>Query.cs` |
| Response DTOs | `<Action><Resource>Response.cs` |
| Validators | `<Action><Resource>Validator.cs` |
| Private fields | `_camelCase` (underscore prefix) |
| Interfaces | `I<Name>` |
| Async methods | `<Name>Async` suffix (except minimal API delegates) |
| Integration events | `<Subject><PastTense>V<N>` e.g. `ShipmentCreatedV1` |
| ADRs | `ADR-NNN-kebab-title.md` |
| Migrations | `<YYYYMMDD>_<MeaningfulName>` |

---

## Prohibited Patterns (Section 0 of Spec)

The agent MUST NOT:

- Use `.Result`, `.Wait()`, or sync-over-async anywhere.
- Write empty catch blocks (`catch { }` or `catch (Exception) { }`).
- Use `DateTime.UtcNow` in testable business logic — use `TimeProvider` instead.
- Use `AutoMapper` — mapping must be explicit.
- Use a generic repository or generic service over EF Core.
- Expose EF entity objects from API endpoints.
- Put business logic in endpoint delegates or controllers.
- Commit secrets, connection strings, PII, or tokens.
- Use `float` or `double` for monetary values — use `decimal`.
- Create placeholder methods (`TODO`, `NotImplementedException`, fake returns) to pass builds.
- Add microservices, Kubernetes, Azure proprietary services, MassTransit, or Kafka.
- Add AI features before Phase 18.
- *Note:* Rule 10 (no reflection for auto-discovery) is overridden by ADR-006. Reflection IS allowed for endpoint/handler auto-registration.
- *Note:* Optimistic concurrency uses `string ConcurrencyStamp` (overriding `long Version` via ADR-006). Do NOT manually assign or roll `ConcurrencyStamp` inside domain entity methods (`Update`, `Triage`, `Assign`, `Resolve`, etc.). Concurrency token verification and stamp rotation are handled centrally and exclusively by `AppDbContext.ApplyAuditAndConcurrency()` on `SaveChangesAsync()`.

---

## Phase Tracking

| Phase | Name | Status |
|---|---|---|
| 0 | Product and repository foundation | ✅ Complete |
| 1 | Runtime, Aspire, database, observability foundation | ✅ Complete |
| 2 | Tenancy and identity | ✅ Complete |
| 3 | Partners, locations, business calendar | ✅ Complete |
| 4 | Shipment domain | ✅ Complete |
| 5 | Messaging foundation: outbox, inbox, RabbitMQ | ✅ Complete |
| 6 | Tracking ingestion and normalization | ✅ Complete |
| 7 | Exception policy engine and case creation | ✅ Complete |
| 8 | Exception case workflow, tasks, SLA | ✅ Complete |
| 9 | Evidence and secure document pipeline | ✅ Complete |
| 10 | Claim eligibility and draft claims | ✅ Complete |
| 11 | Claim approval, submission, response, appeal | ✅ Complete |
| 12 | Financial recovery and settlement | ✅ Complete |
| 13 | Notifications and realtime operations | ✅ Complete |
| 14 | Reporting and carrier scorecards | ✅ Complete |
| 15 | Frontend production workflow | ✅ Complete |
| 16 | Performance, resilience, and security hardening | ✅ Complete |
| 17 | CI/CD and deployment | ⬜ Next phase |
| 18 | AI assistance & RAG (Version 3 only) | ✅ Complete |

---

## Ambiguity Protocol

When a requirement is ambiguous:

1. Prefer the simplest behavior consistent with business invariants.
2. Record the assumption in `docs/assumptions.md`.
3. Add an ADR if the assumption changes architecture, data ownership, security, or a public contract.
4. Implement the reversible option.
5. Do not block the whole phase for a minor ambiguity.

---

## Key Documentation Locations

| Document | Path |
|---|---|
| Master Specification | `LOGISTICS_EXCEPTION_CARRIER_CLAIMS_MASTER_SPEC.md` |
| Agent Guide | `AGENTS.md` (this file) |
| Changelog | `CHANGELOG.md` |
| Assumptions | `docs/assumptions.md` |
| Glossary | `docs/glossary.md` |
| Test Strategy | `docs/test-strategy.md` |
| ADRs | `docs/adr/` |
| Architecture Diagrams | `docs/diagrams/` |
| API Documentation | `docs/api/` |
| Event Contracts | `docs/events/` |
| Runbooks | `docs/runbooks/` |
| Security Notes | `docs/security/` |
| Performance Reports | `docs/performance/` |
