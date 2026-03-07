# Research: Road to Million Tracker

**Branch**: `001-million-tracker` | **Date**: 2026-03-07  
**Purpose**: Resolve all NEEDS CLARIFICATION items from Technical Context before design

---

## 1. .NET Aspire Orchestration for Blazor WASM + API

**Question**: How does Aspire wire up a standalone Blazor WASM project and an ASP.NET Core API? Does SQLite need Aspire integration? How does the WASM app discover the API URL?

### Decision

Use Option A (fixed API port via `appsettings.Development.json`) for API URL discovery. Aspire manages the AppHost topology but does not inject environment variables into the browser; the WASM app reads the API base URL from its own config file. SQLite requires no Aspire integration.

### Rationale

Aspire's service discovery mechanism works by injecting environment variables into the **Kestrel server process** of each registered project. Because Blazor WASM runs in the browser (not in the server process), it cannot read those environment variables. The simplest correct approach is to pin the API to a fixed port in `launchSettings.json` and reference it in the WASM's `wwwroot/appsettings.Development.json`. This is the standard pattern used in all official Aspire + WASM samples.

### Alternatives Considered

| Option | Verdict |
|--------|---------|
| Config endpoint on WASM Kestrel host (`/_config` endpoint) | Valid; adds one server-side endpoint; overhead not needed for single-user local app |
| YARP reverse proxy (BFF pattern) | Production-robust; significant complexity overhead for a personal tracker |
| Hosted WASM (API serves WASM static files) | Simpler integration but merges two projects; loses independent deployability |
| Aspire CommunityToolkit SQLite resource | Unnecessary; SQLite is a local file with a direct connection string |

### Key Details

- **Projects**: `RoadToMillion.AppHost` (orchestrator), `RoadToMillion.ServiceDefaults` (shared telemetry/health), `RoadToMillion.Api` (references ServiceDefaults), `RoadToMillion.Web` (no ServiceDefaults — browser-side)
- **AppHost** calls `builder.AddProject<Projects.RoadToMillion_Api>("api")` and `builder.AddProject<Projects.RoadToMillion_Web>("web").WithReference(api).WaitFor(api)`
- **WASM API URL**: stored in `RoadToMillion.Web/wwwroot/appsettings.Development.json` as `"ApiBaseUrl": "https://localhost:7100"`. The API's `launchSettings.json` pins port 7100.
- **CORS**: The API must configure a CORS policy allowing the WASM origin (e.g., `https://localhost:7200`) for local development.
- **SQLite**: connection string in `RoadToMillion.Api/appsettings.json`. No AppHost entry needed.

---

## 2. CSV Parsing Library

**Question**: What library/approach should be used to parse uploaded CSV files, support multi-delimiter auto-detection, validate headers per-row, and handle Swedish balance values?

### Decision

Use **CsvHelper** (NuGet: `CsvHelper`).

### Rationale

CsvHelper correctly handles quoted fields — a critical requirement for user-uploaded files from unknown legacy systems (e.g., `"Nordnet, ISK"` is a valid account name that splits incorrectly with a naïve `string.Split`). Manual splitting silently corrupts quoted data. `TextFieldParser` has no async support, lives in the `Microsoft.VisualBasic` namespace, and has no typed mapping. Sylvan.Data.Csv is faster but uses a `DbDataReader` columnar API that is ergonomically awkward for a simple 3-column mapping at ≤500 rows. CsvHelper is the ecosystem standard and the correct choice for this use case.

### Alternatives Considered

| Option | Verdict |
|--------|---------|
| Manual `string.Split` | Rejected — silently corrupts quoted fields; unacceptable for user data |
| `TextFieldParser` (Microsoft.VisualBasic) | Rejected — no async, legacy namespace, no typed mapping |
| Sylvan.Data.Csv | Rejected — columnar API awkward for simple mapping; no benefit at 500 rows |

### Key Implementation Details

- **Delimiter auto-detection**: Read the first line, count occurrences of `,`, `;`, `\t` outside quoted regions (15-line implementation). Highest count wins. Reset stream to position 0 before passing to `CsvReader`.
- **Stream handling**: `IFormFile.OpenReadStream()` returns a non-seekable stream. Copy to `MemoryStream` first (fine for ≤500 rows) to allow delimiter detection to reset position.
- **Header validation**: Call `reader.ReadAsync()` + `reader.ReadHeader()`, then check `reader.HeaderRecord` as a case-insensitive `HashSet<string>` against `["AccountGroup", "AccountName"]`. Collect all missing columns before returning error.
- **Balance parsing**: Use `decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out _)` as primary attempt; fall back to `CultureInfo.GetCultureInfo("sv-SE")` for Swedish comma-decimal notation (e.g., `"12 345,67"`).
- **Unknown columns**: Silently ignored. Do not reject the file for extra columns.
- **CsvHelper version**: v33.x supports `net10.0` without issues.

---

## 3. Testing Stack

**Question**: What testing framework and project structure should be used for a .NET 10 API + Blazor WASM solution?

### Decision

**xUnit** as the test framework. **`WebApplicationFactory<TProgram>`** for API integration tests. **bUnit** for Blazor WASM component tests. Two separate test projects.

### Rationale

xUnit is the framework used in all official ASP.NET Core 10 documentation and samples. It is the .NET Foundation standard and has the best `IClassFixture<T>` support for `WebApplicationFactory`. bUnit is the only purpose-built, maintained library for headless Blazor component testing — no browser spin-up required, works with xUnit. The EF InMemory provider is explicitly discouraged by Microsoft for integration tests (no transactions, no raw SQL, schema drift); SQLite in-memory (`DataSource=:memory:`) is used instead since the production DB is already SQLite.

### Alternatives Considered

| Decision | Alternative | Why Not |
|----------|-------------|---------|
| xUnit | NUnit / MSTest | No advantage; xUnit is what MS docs use |
| WebApplicationFactory | Playwright (API) | Playwright is browser E2E; WAF is in-process and faster |
| bUnit | Playwright (Blazor) | Playwright is slow E2E; bUnit is unit-level component testing |
| SQLite in-memory | EF InMemory provider | MS explicitly discourages InMemory for integration tests |

### Project Structure

```
tests/
├── RoadToMillion.Api.Tests/     # WebApplicationFactory, endpoint + CSV import integration tests
└── RoadToMillion.Web.Tests/     # bUnit Blazor component tests (dashboard, forms, progress bar)
```

### Key NuGet Packages

**Api.Tests**: `Microsoft.AspNetCore.Mvc.Testing`, `xunit`, `xunit.runner.visualstudio`, `Microsoft.EntityFrameworkCore.Sqlite`, `FluentAssertions`, `NSubstitute`  
**Web.Tests**: `bunit`, `xunit`, `xunit.runner.visualstudio`, `FluentAssertions`, `NSubstitute`

---

## Summary of Resolved Decisions

| Item | Resolution |
|------|------------|
| Aspire + Blazor WASM API URL | Fixed port in `appsettings.Development.json`; CORS policy on API |
| SQLite + Aspire | No Aspire integration; connection string in `appsettings.json` |
| CSV parsing library | CsvHelper v33.x |
| Delimiter auto-detection | Count `,`/`;`/`\t` outside quotes in first line |
| Balance decimal parsing | Invariant culture primary, sv-SE fallback |
| Test framework | xUnit |
| API integration testing | WebApplicationFactory + SQLite in-memory |
| Blazor component testing | bUnit |
| Test project count | Two: Api.Tests and Web.Tests |
