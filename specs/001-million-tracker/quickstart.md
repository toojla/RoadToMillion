# Quickstart: Road to Million Tracker

**Branch**: `001-million-tracker` | **Date**: 2026-03-07

---

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [.NET Aspire workload](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/setup-tooling): `dotnet workload install aspire`
- Visual Studio 2022 17.12+ or VS Code with C# Dev Kit

---

## Project Layout

```
RoadToMillion/
├── RoadToMillion.sln
├── RoadToMillion.AppHost/          ← Start here — Aspire orchestrator
├── RoadToMillion.ServiceDefaults/  ← Shared telemetry & health checks
├── RoadToMillion.Api/              ← ASP.NET Core Web API (port 7100)
├── RoadToMillion.Web/              ← Blazor WebAssembly frontend (port 7200)
└── tests/
    ├── RoadToMillion.Api.Tests/
    └── RoadToMillion.Web.Tests/
```

---

## Running the Application

### Option 1 — Command Line

```bash
cd RoadToMillion/RoadToMillion.AppHost
dotnet run
```

Aspire starts all components and opens the **Aspire Dashboard** automatically at `https://localhost:15888`. From there you can see logs, traces, and links to each running service.

The Blazor frontend will be available at **`https://localhost:7200`**.

### Option 2 — Visual Studio

Set `RoadToMillion.AppHost` as the startup project and press **F5**. The Aspire dashboard and all services start together.

### Option 3 — VS Code

Use the **C# Dev Kit** Run & Debug panel. Select the `AppHost` launch configuration.

---

## First Run

On first startup the API creates the SQLite database file at:

```
RoadToMillion.Api/roadtomillion.db
```

No manual migration steps are required. EF Core applies all pending migrations automatically on startup.

---

## API

The REST API is available at **`https://localhost:7100`**.

Interactive documentation (Swagger UI) is available in Development mode at:

```
https://localhost:7100/swagger
```

---

## Configuration

### API base URL (Blazor WASM)

The WASM app reads the API URL from:

```
RoadToMillion.Web/wwwroot/appsettings.Development.json
```

Default value:

```json
{
  "ApiBaseUrl": "https://localhost:7100"
}
```

If you change the API port, update this file to match.

### SQLite connection string

```
RoadToMillion.Api/appsettings.json
```

```json
{
  "ConnectionStrings": {
    "sqlite": "Data Source=./roadtomillion.db"
  }
}
```

---

## Running Tests

```bash
# All tests
dotnet test

# API integration tests only
dotnet test tests/RoadToMillion.Api.Tests

# Blazor component tests only
dotnet test tests/RoadToMillion.Web.Tests
```

Tests use an in-memory SQLite database — no external dependencies required.

---

## Resetting Data

Delete the database file to start fresh:

```bash
rm RoadToMillion.Api/roadtomillion.db
```

The database is re-created automatically on the next startup.
