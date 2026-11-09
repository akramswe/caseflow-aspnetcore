# Architecture and design rationale

## Request flow

```text
Browser UI (wwwroot/app.js)
       | HTTP / JSON
       v
ASP.NET Core Minimal API (Endpoints/CaseEndpoints.cs)
       |
       v
CaseService (application use cases)
       |            |
       |            +--> CaseValidation / CaseWorkflow (domain rules)
       v
CaseDbContext (EF Core) ---> SQLite file
       |
       +--> CaseNote + CaseActivity (persisted history)
```

## Domain

- **SupportCase** is the primary aggregate: a customer request with a reference, priority, status, assignee, SLA, and mutation version.
- **CaseNote** is an internal case comment.
- **CaseActivity** is an append-only *application-level* activity record. It is not a tamper-proof legal audit log.
- **CaseWorkflow** defines allowed transitions and default SLA deadlines, independently of the UI.

## Optimistic concurrency

Each case has an integer `Version` configured as an EF Core concurrency token.

1. Client reads case `version = 4`.
2. Client submits a mutation with `version = 4`.
3. Service checks it against the current version and increments the entity's `Version`.
4. EF Core includes the previously read concurrency token in its update condition.
5. A stale client version, or a concurrent database write, yields `409 Conflict` instead of overwriting the other change.

This is particularly useful when multiple team members can manage the same work item.

## Design trade-offs

| Decision | Why it works for a portfolio | What to improve for production |
| --- | --- | --- |
| Minimal APIs | Less boilerplate for a focused web-oriented application | Organize by features as the project scales |
| SQLite | No separate server; integration tests use real SQL | SQL Server / PostgreSQL for concurrent deployment |
| `EnsureCreatedAsync` | First-run setup without migration commands | Replace with versioned EF migrations |
| Small service layer | Explicit and testable business operations | Add interfaces if multiple implementations/use cases justify them |
| Vanilla JavaScript | No npm dependency, easy to inspect HTTP interactions | Adopt an appropriate SPA/framework when complexity warrants it |
| Fictional seeded data | Immediately browsable demonstration | Replace with controlled fixture/import strategy |
| Internal notes | Demonstrates append-only collaboration data | Add authentication, role permissions, and data protection |

## Testing approach

- `CaseWorkflowTests`: legal/illegal transitions, SLA calculations, overdue semantics, CSV formula safety.
- `CaseServiceTests`: persist cases with in-memory SQLite, validation, conflict behavior, notes+activities, filtering, dashboard counts.
- CI builds and runs tests after pushes and pull requests.

## Explicit non-goals

The project does **not** implement Dataverse connectivity, a Dynamics 365 add-on, actual customer authentication, multi-tenancy, production deployment hardening, or a background scheduler. Those would be future features rather than claims about the current codebase.
