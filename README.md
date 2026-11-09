# CaseFlow — Customer Support & Workflow Management

A **C# / ASP.NET Core 10** portfolio project for managing customer cases in a business software environment. It includes a browser dashboard, REST endpoints, an SQLite database, explicit workflow rules, optimistic concurrency, activity history, automated tests, CI, and Docker support.

> **Independent demo.** CaseFlow is not affiliated with PTM, Microsoft Dynamics 365, or Power Platform. There is **no Dynamics 365 integration** in this prototype. Customers and addresses in seed data are fictional.

## Why build it?

This project demonstrates an end-to-end feature lifecycle: design a small business domain, implement a web API and frontend, validate input, enforce business rules, test core behavior, persist data, and support ongoing maintenance. Those are practical skills for a software developer working on business applications and extensible products.

## Features

- **Case lifecycle** with allowed transitions: `Open → InProgress → WaitingOnCustomer / Resolved → Closed`; reopened cases return to `InProgress`.
- **Priorities and SLA deadlines**: Critical 4h, High 24h, Normal 72h, Low 7d. These are sample policies, not any company's real SLAs.
- **Customer-facing data model** with a unique case reference, customer identity, assignee, timestamps, and version.
- **Search/filter/pagination** by customer, reference, title, status, or priority.
- **Audit trail and internal notes** with actor and timestamp.
- **Conflict protection** via a `Version` concurrency token; stale updates return HTTP `409`.
- **Dashboard** for case totals, workflow counts, overdue cases, and critical cases.
- **CSV export** with quoted fields and basic spreadsheet-formula injection protection.
- **Responsive web UI** built with vanilla JavaScript and CSS; no frontend build chain required.
- **xUnit tests**, GitHub Actions CI, and multi-stage Docker image.

## Tech stack

| Layer | Technology |
| --- | --- |
| Language | C# |
| Web | ASP.NET Core 10 Minimal APIs |
| ORM | Entity Framework Core 10 |
| Storage | SQLite (persistent file; in-memory SQLite in tests) |
| UI | HTML, CSS, vanilla JavaScript |
| Tests | xUnit, Microsoft.NET.Test.Sdk |
| Operations | Health endpoint, Docker, GitHub Actions |

## Quick start

**Prerequisite:** [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```bash
# From the repository root
dotnet restore src/CaseFlow.Api/CaseFlow.Api.csproj
dotnet run --project src/CaseFlow.Api/CaseFlow.Api.csproj --urls http://localhost:5055
```

Open **http://localhost:5055** in your browser. On first launch, the API creates `data/caseflow.db` in the application's content directory and adds six fictional cases. All subsequent changes persist across restarts.

> The built-in sample database is auto-created via `EnsureCreatedAsync`, deliberately avoiding a migrations setup step. For a real deployment, replace this with EF Core migrations and a more appropriate database solution.

### Automated tests

```bash
dotnet restore tests/CaseFlow.Tests/CaseFlow.Tests.csproj
dotnet test tests/CaseFlow.Tests/CaseFlow.Tests.csproj
```

Unit tests cover workflow transitions and SLA calculation. Database-backed tests use **real in-memory SQLite**, not mocked SQL behavior, and exercise create, validation, transitions, concurrency, audit history, filtering, and overdue dashboard counts.

### Run with Docker

```bash
docker compose up --build
```

Open **http://localhost:5055**. SQLite data persists in a named Docker volume. Stop with `docker compose down`; `docker compose down -v` also deletes stored demo data.

## Main API routes

| Method | Route | Purpose |
| --- | --- | --- |
| GET | `/health` | Database readiness |
| GET | `/api/dashboard` | Summary metrics |
| GET | `/api/cases` | Search, filter, paginate |
| GET | `/api/cases/{id}` | Case details, notes, activity |
| POST | `/api/cases` | Create a case |
| PUT | `/api/cases/{id}/status` | Change status with version check |
| PUT | `/api/cases/{id}/assignment` | Assign or unassign |
| POST | `/api/cases/{id}/notes` | Add an internal note |
| GET | `/api/cases/export.csv` | Export cases as CSV |

See [`docs/API.md`](docs/API.md) for example requests and responses, or use [`CaseFlow.Api.http`](CaseFlow.Api.http) in an HTTP client.

## Project structure

```text
CaseFlow-PTM-Portfolio/
├── src/CaseFlow.Api/
│   ├── Contracts/        # Request and response records
│   ├── Domain/           # Case entities, enums
│   ├── Services/         # Workflow, validation, application logic
│   ├── Data/             # EF Core mappings and fictional sample data
│   ├── Endpoints/        # HTTP route definitions
│   ├── wwwroot/          # No-build frontend
│   └── Program.cs        # Composition root
├── tests/CaseFlow.Tests/ # Domain + SQLite-backed service tests
├── docs/                # Architecture, API, interview notes
├── .github/workflows/    # CI pipeline
├── Dockerfile
└── compose.yaml
```

## Design decisions

- **Clear boundaries:** HTTP endpoints do transport work; `CaseService` owns application operations; `CaseWorkflow` owns business transition/SLA rules; `CaseDbContext` handles persistence.
- **Optimistic concurrency:** every mutation sends the last `version`; stale requests return `409` instead of silently overwriting colleagues' edits.
- **Auditability:** every create, assignment, note, or status change adds a timestamped event.
- **Server-enforced rules:** the client shows permitted transitions, but the backend independently validates them.
- **Demo-friendly footprint:** SQLite and static HTML minimize setup, keeping the emphasis on C# and business logic.

## Security and production gaps (important)

**This is a local portfolio prototype, not a production-ready multi-tenant CRM.** It intentionally has no authentication, authorization, role-based access control, tenant isolation, rate limiting, encrypted secrets handling, production-grade migrations, or customer-data retention policy. **Do not expose it publicly or enter real customer information.** Before production use: introduce Microsoft Entra ID / OAuth, tenant-aware permissions, audit protections, structured observability, migrations, and a managed database; implement genuine Dynamics 365 integration only after reviewing Microsoft Dataverse APIs and permissions.


