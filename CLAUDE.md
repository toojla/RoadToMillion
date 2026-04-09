# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Build
dotnet build

# Run full stack (recommended) — starts PostgreSQL, pgAdmin, API, and Web via Aspire
dotnet run --project orchestration/RoadToMillion.AppHost

# Run API or Web individually
dotnet run --project src/RoadToMillion.Api
dotnet run --project src/RoadToMillion.Web

# Run all tests
dotnet test

# Run a specific test class
dotnet test --filter "FullyQualifiedName~AccountGroupServiceTests"

# Add a database migration (run from API project dir)
cd src/RoadToMillion.Api
dotnet ef migrations add <MigrationName>
```

Migrations apply automatically on startup via `db.Database.Migrate()`. The API runs on `https://localhost:7100`, Web on `https://localhost:7200`.

## Architecture

Personal finance tracking app. Single-user, JWT-authenticated. Stack: .NET 10, ASP.NET Core Minimal APIs, Blazor WebAssembly, EF Core + PostgreSQL, .NET Aspire for orchestration.

**Data hierarchy:** `AccountGroup` → `Account` → `BalanceSnapshot`. Cascade deletes are configured.

**Request flow:** Blazor WASM → `ApiClient` (injects JWT from localStorage) → Minimal API endpoints → Service layer → EF Core / PostgreSQL.

**Key cross-cutting patterns:**
- **Result pattern** (`Result.cs`) — all services return `Result<T>` (Success/Failure), never throw for business errors.
- **Minimal APIs** — no controllers; endpoints registered via extension methods grouped by domain (Auth, Portfolio, AccountGroup, Account, Snapshot, Import).
- **Service interfaces** — every service has an interface; implementations registered in `ServiceCollectionExtensions.cs`.
- **Global usings** — declared in `GlobalUsings.cs` in the API project.

**Auth:** ASP.NET Core Identity + JWT Bearer. Token blacklist (singleton, in-memory) handles logout revocation. Rate limiting on auth endpoints (10 req/min). Account lockout after 5 failed attempts (15 min). Registration can be toggled via `Features:EnableUserRegistration` in `appsettings.json`.

**Aspire AppHost** configures PostgreSQL with a persistent Docker volume, pgAdmin, and wires the API/Web with correct dependency ordering. Service discovery and OpenTelemetry are provided by `RoadToMillion.ServiceDefaults`.

**CSV import:** uses CsvHelper with row-level validation, returns warnings alongside results.

## Testing

Framework: xUnit + NSubstitute (mocking) + Shouldly (assertions). EF Core InMemory provider is used for data access tests. 4 tests are intentionally skipped (transaction-dependent scenarios).

When changing a service or validator, update the corresponding test class in `tests/RoadToMillion.UnitTests/`.

## Workflow Conventions

- Complete tasks in the order specified unless there is a clear dependency reason not to.
- After changes to services, validators, or shared code, check and update related unit tests before considering the task complete.
- Never use file-local types (`file class`) in method signatures visible outside the file — this causes C# compilation errors.
- Prefer user-friendly error messages and graceful handling over throwing raw exceptions, especially in web-facing and Blazor contexts.
