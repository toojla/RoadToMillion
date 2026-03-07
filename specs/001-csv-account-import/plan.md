# Implementation Plan: Road to Million Tracker

**Branch**: `001-million-tracker` | **Date**: 2026-03-07 | **Spec**: [spec.md](../001-million-tracker/spec.md)  
**Input**: Feature specification from `/specs/001-million-tracker/spec.md`

> **Note**: This plan covers the full Road to Million Tracker feature (including CSV import). All design artifacts are under `/specs/001-million-tracker/`.

## Summary

Build a single-user portfolio tracker toward 1,000,000 SEK. The system consists of an ASP.NET Core Web API (`RoadToMillion.Api`) backed by EF Core + SQLite, a standalone Blazor WebAssembly frontend (`RoadToMillion.Web`), and a .NET Aspire orchestrator (`RoadToMillion.AppHost`). Key capabilities: account group and account management, balance snapshot recording, a live progress dashboard, and CSV import of accounts from a previous system with a preview-before-commit flow.

## Technical Context

**Language/Version**: C# / .NET 10  
**Primary Dependencies**: ASP.NET Core (Web API), EF Core 10, Blazor WebAssembly, .NET Aspire, CsvHelper  
**Storage**: SQLite (via EF Core; file `roadtomillion.db` in API project directory)  
**Testing**: xUnit, WebApplicationFactory (API integration tests), bUnit (Blazor component tests)  
**Target Platform**: Web browser (Blazor WASM) + local Kestrel server  
**Project Type**: Web application (REST API + WASM frontend)  
**Performance Goals**: Dashboard load < 2s; CSV preview (500 rows) < 5s; account creation visible < 60s  
**Constraints**: Single user; all values in SEK; import is atomic (full commit or full rollback); CORS required between WASM origin and API  
**Scale/Scope**: Single user; up to 50+ accounts; up to 500 rows per CSV import

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

> ⚠️ The project constitution (`.specify/memory/constitution.md`) has not been customized for this project — it still contains template placeholder content. No project-specific rules or gates are defined. All constitution gates are marked **PASS (N/A)** until the constitution is authored.

| Gate | Status | Notes |
|------|--------|-------|
| Architecture principles | PASS (N/A) | Constitution not configured |
| Complexity constraints | PASS (N/A) | Constitution not configured |
| Testing requirements | PASS (N/A) | Constitution not configured |

**Post-design re-check**: Still PASS (N/A) — no violations to track.

## Project Structure

### Documentation (this feature)

```text
specs/001-million-tracker/
├── spec.md              ← Authoritative feature specification (merged)
├── plan.md              ← (pointer: actual plan is at specs/001-csv-account-import/plan.md)
├── research.md          ← Phase 0 output
├── data-model.md        ← Phase 1 output
├── quickstart.md        ← Phase 1 output
├── contracts/
│   └── api.md           ← HTTP API contract
└── checklists/
    └── requirements.md
```

### Source Code (repository root)

```text
RoadToMillion/
├── RoadToMillion.slnx
├── RoadToMillion.AppHost/          ← Aspire orchestrator (NEW)
│   └── Program.cs
├── RoadToMillion.ServiceDefaults/  ← Shared telemetry & health checks (NEW)
│   └── Extensions.cs
├── RoadToMillion.Api/              ← ASP.NET Core Web API, port 7100 (NEW)
│   ├── Program.cs
│   ├── appsettings.json            ← SQLite connection string
│   ├── Data/
│   │   ├── AppDbContext.cs
│   │   └── Migrations/
│   ├── Endpoints/                  ← Minimal API endpoint groups
│   │   ├── AccountGroupEndpoints.cs
│   │   ├── AccountEndpoints.cs
│   │   ├── SnapshotEndpoints.cs
│   │   ├── PortfolioEndpoints.cs
│   │   └── ImportEndpoints.cs
│   ├── Models/                     ← Entity classes
│   └── Services/
│       └── CsvImportService.cs
├── RoadToMillion.Web/              ← Blazor WASM frontend, port 7200 (EXISTS)
│   ├── Program.cs
│   ├── Pages/
│   │   ├── Home.razor              ← Dashboard (progress bar, totals)
│   │   ├── AccountGroups.razor     ← List + create groups
│   │   ├── AccountDetail.razor     ← Account snapshots + record new
│   │   └── Import.razor            ← CSV upload, preview, confirm
│   ├── Services/
│   │   └── ApiClient.cs            ← Typed HttpClient wrapper
│   └── wwwroot/
│       └── appsettings.Development.json  ← ApiBaseUrl: https://localhost:7100
└── tests/                          ← Designed but OUT OF SCOPE for iteration 1 (no test tasks in tasks.md)
    ├── RoadToMillion.Api.Tests/    ← API integration tests (planned for future iteration)
    │   └── (WebApplicationFactory, xUnit, SQLite in-memory)
    └── RoadToMillion.Web.Tests/   ← Blazor component tests (planned for future iteration)
        └── (bUnit, xUnit, mocked ApiClient)
```

**Structure Decision**: Multi-project web application (Option 2 variant). Separate `Api` and `Web` projects are required by the spec (FR-016: all operations via API). `AppHost` and `ServiceDefaults` are standard Aspire projects. Two test projects align with Microsoft's recommendation to separate integration from component tests.

## Complexity Tracking

No constitution violations — table not applicable.
