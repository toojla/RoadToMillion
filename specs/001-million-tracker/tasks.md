# Tasks: Road to Million Tracker

**Input**: Design documents from `specs/001-million-tracker/`  
**Date**: 2026-03-07  
**Tech Stack**: C# / .NET 10, ASP.NET Core (Minimal API), EF Core 10 + SQLite, Blazor WASM, .NET Aspire, CsvHelper, xUnit  
**No tests requested** — test tasks are omitted per spec (no TDD requirement stated)

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: Parallelizable (different files, no incomplete dependency)
- **[US#]**: Maps to user story from spec.md

---

## Phase 1: Setup (Project Initialization)

**Purpose**: Create the solution structure and new projects that do not yet exist.

- [X] T001 Create `RoadToMillion.AppHost` Aspire orchestrator project and add to `RoadToMillion.slnx`
- [X] T002 Create `RoadToMillion.ServiceDefaults` shared library project and add to `RoadToMillion.slnx`
- [X] T003 [P] Create `RoadToMillion.Api` ASP.NET Core Web API project (net10.0) and add to `RoadToMillion.slnx`
- [X] T004 [P] Add NuGet packages to `RoadToMillion.Api`: `Microsoft.EntityFrameworkCore.Sqlite`, `Microsoft.EntityFrameworkCore.Design`, `CsvHelper`, `Microsoft.AspNetCore.OpenApi`
- [X] T005 Add project references in `RoadToMillion.AppHost/RoadToMillion.AppHost.csproj` to both `RoadToMillion.Api` and `RoadToMillion.Web`
- [X] T006 [P] Add `RoadToMillion.ServiceDefaults` project reference to `RoadToMillion.Api`; call `builder.AddServiceDefaults()` and `app.MapDefaultEndpoints()` in `RoadToMillion.Api/Program.cs`
- [X] T007 [P] Clean up `RoadToMillion.Web`: remove sample `Counter.razor`, `Weather.razor` pages and `weather.json`; update `NavMenu.razor` with placeholder links for Dashboard, Accounts, Import

**Checkpoint**: Solution builds with all projects referenced. `dotnet run` on `RoadToMillion.AppHost` starts the Aspire dashboard.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: EF Core data layer, Blazor HTTP client wiring, and CORS — must be complete before any user story can be implemented.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T008 Create `AccountGroup` entity in `RoadToMillion.Api/Models/AccountGroup.cs` (Id, Name; navigation: ICollection&lt;Account&gt;)
- [X] T009 [P] Create `Account` entity in `RoadToMillion.Api/Models/Account.cs` (Id, AccountGroupId FK, Name, Description?; navigation: AccountGroup, ICollection&lt;BalanceSnapshot&gt;)
- [X] T010 [P] Create `BalanceSnapshot` entity in `RoadToMillion.Api/Models/BalanceSnapshot.cs` (Id, AccountId FK, Amount decimal, Date DateOnly, RecordedAt DateTime UTC)
- [X] T011 Create `AppDbContext` in `RoadToMillion.Api/Data/AppDbContext.cs`: register all three entities; configure cascade-delete from AccountGroup to Account and from Account to BalanceSnapshot (deleting a parent deletes its children, not the reverse); add case-insensitive unique index on AccountGroup.Name and composite unique index on (Account.AccountGroupId, Account.Name)
- [X] T012 Add initial EF Core migration (`dotnet ef migrations add InitialCreate -p RoadToMillion.Api`); configure SQLite connection string `"Data Source=./roadtomillion.db"` in `RoadToMillion.Api/appsettings.json`; call `db.Database.Migrate()` on startup in `RoadToMillion.Api/Program.cs`
- [X] T013 Configure CORS in `RoadToMillion.Api/Program.cs`: add policy allowing origin `https://localhost:7200` with any method and header; apply with `app.UseCors()`
- [X] T014 [P] Create `ApiClient` typed HttpClient wrapper in `RoadToMillion.Web/Services/ApiClient.cs` with injected `HttpClient`; add stub methods for each endpoint group (portfolio, account groups, accounts, snapshots, import) returning `Task` placeholders
- [X] T015 [P] Create `RoadToMillion.Web/wwwroot/appsettings.Development.json` with `"ApiBaseUrl": "https://localhost:7100"`; register named `HttpClient` with `BaseAddress` read from configuration in `RoadToMillion.Web/Program.cs`; inject `ApiClient` as scoped service

**Checkpoint**: API starts, applies migration, and responds to requests. Blazor app starts, reads config, and `ApiClient` is injectable in components.

---

## Phase 3: User Story 1 — View Progress Dashboard (Priority: P1) 🎯 MVP

**Goal**: User opens the app and sees total portfolio value, progress toward 1,000,000 SEK, remaining amount, and per-group subtotals — with pre-seeded data.

**Independent Test**: Seed two account groups with accounts and balance snapshots directly in SQLite; open `https://localhost:7200`; verify current total, remaining, progress bar percentage, and per-group subtotals all display correctly. Verify empty-state prompt when no data exists.

- [X] T016 [US1] Implement `GET /api/portfolio/summary` in `RoadToMillion.Api/Endpoints/PortfolioEndpoints.cs`: query most-recent BalanceSnapshot per Account (by Date, tiebreak RecordedAt desc), sum per group and overall; return `PortfolioSummary` JSON with `currentTotal`, `goalAmount` (1000000), `remainingAmount`, `progressPercentage` (capped 0–100), and `groups[]`; register endpoint group in `Program.cs`
- [X] T017 [US1] Build `RoadToMillion.Web/Pages/Home.razor` dashboard: call `ApiClient.GetPortfolioSummaryAsync()`, display current total (formatted SEK), progress bar (`width: {pct}%`), remaining amount, per-group subtotals table; show empty-state prompt with link to Accounts page when `currentTotal` is 0 and no groups exist

**Checkpoint**: Dashboard loads, shows all values correctly with seeded data, shows empty state when DB is empty.

---

## Phase 4: User Story 2 — Manage Account Groups and Accounts (Priority: P2)

**Goal**: User can create account groups and accounts, see them listed with current balances, and delete them (with cascade).

**Independent Test**: Navigate to the Accounts page; create a group "Avanza"; add account "ISK" under it; verify both appear; delete the account; verify it is gone. Attempt to create a group with a blank name; verify error message appears.

- [X] T018 [P] [US2] Implement account group endpoints in `RoadToMillion.Api/Endpoints/AccountGroupEndpoints.cs`: `GET /api/account-groups` (list with currentTotal per group); `POST /api/account-groups` (validate non-blank name, 409 on duplicate, return 201 with Location); `DELETE /api/account-groups/{id}` (cascade via EF; 404 if not found); register in `Program.cs`
- [X] T019 [P] [US2] Implement account endpoints in `RoadToMillion.Api/Endpoints/AccountEndpoints.cs`: `GET /api/account-groups/{groupId}/accounts` (list with currentBalance and hasSnapshots); `POST /api/account-groups/{groupId}/accounts` (validate non-blank name, 409 on duplicate within group, return 201); `DELETE /api/accounts/{id}` (cascade; 404 if not found); register in `Program.cs`
- [X] T020 [US2] Build `RoadToMillion.Web/Pages/AccountGroups.razor`: load and display all groups with subtotals; inline create-group form (name field, blank-name error display); per-group expandable account list showing name, description, current balance or "No snapshots yet"; inline create-account form per group; delete buttons for groups and accounts with confirm prompt; refresh list after each mutation

**Checkpoint**: Full CRUD for groups and accounts works end-to-end. Dashboard total updates after adding/removing accounts.

---

## Phase 5: User Story 3 — Record Balance Snapshots (Priority: P3)

**Goal**: User navigates to an account, records a balance with a date, sees the history list update, and verifies the dashboard total reflects the new value.

**Independent Test**: Navigate to a specific account detail view; submit a snapshot with amount 50000 and today's date; verify it appears at top of history list marked as most recent; navigate to dashboard; verify total increased by 50000.

- [X] T021 [US3] Implement balance snapshot endpoints in `RoadToMillion.Api/Endpoints/SnapshotEndpoints.cs`: `GET /api/accounts/{accountId}/snapshots` (all snapshots ordered by Date desc, then RecordedAt desc; include `isMostRecent` flag); `POST /api/accounts/{accountId}/snapshots` (validate amount > 0, required date; set RecordedAt = UtcNow; return 201); register in `Program.cs`
- [X] T022 [US3] Build `RoadToMillion.Web/Pages/AccountDetail.razor`: display account name and description; load and render snapshot history table (date, amount, "Current" badge on most-recent row); record-new-snapshot form with amount input and date picker; show validation error for zero/negative amount; navigate here from AccountGroups.razor account list

**Checkpoint**: Snapshots can be recorded per account. Dashboard total correctly reflects only the most-recent snapshot per account.

---

## Phase 6: User Story 4 — Import Accounts from CSV File (Priority: P4)

**Goal**: User uploads a CSV file; accounts and groups are parsed and imported into the tracker without manual entry.

**Independent Test**: Prepare a CSV with two groups and five accounts with balances. Upload via the Import page, confirm, verify all groups and accounts appear in the Accounts view and dashboard total reflects the imported balances.

- [X] T023 [US4] Implement `CsvImportService.ParsePreviewAsync(IFormFile)` in `RoadToMillion.Api/Services/CsvImportService.cs`: copy stream to `MemoryStream`; auto-detect delimiter by counting `,`/`;`/`\t` occurrences outside quotes in the first line; use CsvHelper with detected delimiter and `CultureInfo.InvariantCulture`; group rows by AccountGroup name (case-insensitive); for each group, check if it already exists in DB; for each account, check for existing account in same group; parse optional Balance column (try InvariantCulture, fallback sv-SE); return `ImportPreview` with groups, accounts, initialBalances, alreadyExists flags, willBeSkipped flags, warnings, row counts
- [X] T024 [US4] Implement `POST /api/import/preview` in `RoadToMillion.Api/Endpoints/ImportEndpoints.cs`: accept `multipart/form-data` with `file` field; call `CsvImportService.ParsePreviewAsync`; return `ImportPreview` JSON (200); register in `Program.cs`
- [X] T025 [US4] Build file-upload step in `RoadToMillion.Web/Pages/Import.razor`: file input accepting `.csv`; on selection upload to `POST /api/import/preview`; display resulting preview table — groups (with alreadyExists badge), accounts per group (with balance, skipped indicator), warning list with row numbers; show row counts (total / valid / skipped)

**Checkpoint**: Uploading a valid CSV returns a correct preview table showing all groups, accounts, balances, and conflict flags.

---

## Phase 7: User Story 5 — Preview and Confirm Before Import (Priority: P5)

**Goal**: User reviews the parsed preview and either confirms (data is saved atomically) or cancels (no change to system state).

**Independent Test**: Upload a CSV that conflicts with one existing group. Verify the preview shows the conflict. Cancel — verify no data changed. Re-upload and confirm — verify only new groups/accounts are created, existing ones skipped, ImportResult summary shows correct counts.

- [X] T026 [US5] Implement `CsvImportService.ExecuteImportAsync(ImportPreview)` in `RoadToMillion.Api/Services/CsvImportService.cs`: open a single EF Core transaction; for each group in preview not marked alreadyExists, create AccountGroup; for each account not marked willBeSkipped, create Account under the appropriate group (new or fetched existing); for accounts with a non-null initialBalance, create BalanceSnapshot dated today (UtcNow date, RecordedAt = UtcNow); commit; return ImportResult with counts; rollback entire transaction on any error
- [X] T027 [US5] Implement `POST /api/import/confirm` in `RoadToMillion.Api/Endpoints/ImportEndpoints.cs`: accept `ImportPreview` JSON body; validate it contains at least one non-skipped account; **before opening a transaction**, re-query the DB for each AccountGroup name in the preview that was **not** marked `alreadyExists` — if any of those names now exist in the DB, the data changed since preview was generated; return 409 with body `{"error": "Data changed since preview was generated. Please re-upload and preview again."}` without writing anything; otherwise call `CsvImportService.ExecuteImportAsync`; return `ImportResult` (200)
- [X] T028 [US5] Add confirm/cancel UI to `RoadToMillion.Web/Pages/Import.razor`: after preview loads, show "Confirm Import" and "Cancel" buttons; Confirm posts preview JSON to `POST /api/import/confirm`, shows ImportResult success banner (groups created, accounts created, snapshots recorded, rows skipped); Cancel resets to file picker with no data written; navigate to Accounts page after successful import

**Checkpoint**: Confirm writes all valid records in one transaction. Cancel leaves system unchanged. Success banner shows correct counts.

---

## Phase 8: User Story 6 — Handle Invalid or Malformed CSV Files (Priority: P6)

**Goal**: User receives clear, specific error messages for every category of invalid file; no data is ever written on error.

**Independent Test**: Upload a PDF → expect file-type error. Upload a CSV missing the AccountName column → expect missing-column error listing the column name. Upload a CSV with only a header row → expect empty-file error. Upload a CSV where row 3 has no AccountName → expect row-level warning in preview, but other rows still appear.

- [X] T029 [US6] Add file-level validation to `CsvImportService.ParsePreviewAsync` in `RoadToMillion.Api/Services/CsvImportService.cs`: check content type and file extension — reject non-CSV with 400 error `"File type not supported. Please upload a CSV file."`; after parsing header, check for required columns `AccountGroup` and `AccountName` (case-insensitive) — if missing, return 400 with `"Required columns are missing: {list}"`; if file has header but zero data rows, return 400 with `"The file is empty or contains no data rows."`
- [X] T030 [US6] Add row-level validation to `CsvImportService.ParsePreviewAsync` in `RoadToMillion.Api/Services/CsvImportService.cs`: for each data row, if AccountGroup or AccountName is blank/whitespace, add an `ImportWarning` (rowNumber, field, message, Severity=Warning) and mark the row skipped; increment `RowsSkipped`; include all warnings in returned `ImportPreview`; allow remaining valid rows to proceed normally
- [X] T031 [US6] Add error and warning display to `RoadToMillion.Web/Pages/Import.razor`: on 400 response from preview endpoint, display the error message prominently (not a preview table); in the preview table, render each row-level warning beneath the relevant group/account entry; highlight skipped accounts in the preview; show warning count in the row-count summary

**Checkpoint**: Every invalid file scenario produces a clear, actionable message. No data is written on any error path.

---

## Phase 9: Polish & Cross-Cutting Concerns

- [X] T032 [P] Add Swagger UI to `RoadToMillion.Api/Program.cs` for Development environment: `builder.Services.AddOpenApi()` and `app.MapOpenApi()` / `app.UseSwaggerUI()`
- [X] T033 [P] Finalize `RoadToMillion.AppHost/Program.cs`: wire `var api = builder.AddProject<Projects.RoadToMillion_Api>("api")` with pinned **HTTPS** port 7100 (use `.WithHttpsEndpoint(7100)` or equivalent Aspire API); wire `var web = builder.AddProject<Projects.RoadToMillion_Web>("web").WithReference(api).WaitFor(api)` with pinned HTTPS port 7200; confirm Aspire dashboard launches at `https://localhost:15888`
- [X] T034 Run full quickstart validation per `specs/001-million-tracker/quickstart.md`: `dotnet run` on AppHost; exercise all six user stories end-to-end (dashboard, create group/account, record snapshot, import CSV, preview + confirm, error handling); verify `roadtomillion.db` persists between restarts

**Checkpoint**: Full application runs from a single `dotnet run` on AppHost. All user stories verified end-to-end.

---

## Dependencies & Execution Order

### Phase Dependencies

| Phase | Depends On | Can Start |
|-------|-----------|-----------|
| Phase 1 — Setup | Nothing | Immediately |
| Phase 2 — Foundational | Phase 1 complete | After T007 |
| Phase 3 — US1 | Phase 2 complete | After T015 |
| Phase 4 — US2 | Phase 2 complete | After T015 (parallel with US1) |
| Phase 5 — US3 | Phase 4 complete | After T020 (needs Account data to exist) |
| Phase 6 — US4 | Phase 4 complete | After T020 (needs AccountGroup/Account to be creatable) |
| Phase 7 — US5 | Phase 6 complete | After T025 |
| Phase 8 — US6 | Phase 6 complete | After T024 (adds validation to same service/endpoint) |
| Phase 9 — Polish | All stories complete | After T031 |

### User Story Dependencies

- **US1 (P1)**: Independent after Foundational — can be implemented as MVP with no other story
- **US2 (P2)**: Independent after Foundational — can run in parallel with US1
- **US3 (P3)**: Depends on US2 (requires accounts to exist to record snapshots against)
- **US4 (P4)**: Depends on US2 (import creates groups and accounts; those endpoints must exist)
- **US5 (P5)**: Depends on US4 (confirm step wraps the preview service)
- **US6 (P6)**: Depends on US4 (validation is layered onto the same service and endpoint)

### Parallel Opportunities Per Phase

**Phase 1**: T003 ∥ T004, then T006 ∥ T007  
**Phase 2**: T008 ∥ T009 ∥ T010 (three entity files), then T014 ∥ T015  
**Phase 3+4**: T016/T017 (US1) ∥ T018/T019/T020 (US2)  
**Phase 4**: T018 ∥ T019 (different endpoint files, same phase)  
**Phase 9**: T032 ∥ T033

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001–T007)
2. Complete Phase 2: Foundational (T008–T015)
3. Complete Phase 3: US1 Dashboard (T016–T017)
4. **STOP and VALIDATE**: Seed data directly into SQLite, open app, verify dashboard
5. Ship/demo the read-only portfolio view

### Incremental Delivery

1. **Setup + Foundational** → Foundation ready
2. **+ US1** (T016–T017) → Read-only dashboard MVP
3. **+ US2** (T018–T020) → Create/delete accounts — app is self-sufficient
4. **+ US3** (T021–T022) → Record balances — live tracker works
5. **+ US4 + US5 + US6** (T023–T031) → CSV import — migration from previous system
6. **Polish** (T032–T034) → Swagger, Aspire config, end-to-end validation

### Summary

| | Count |
|---|---|
| **Total tasks** | 34 |
| Setup (Phase 1) | 7 |
| Foundational (Phase 2) | 8 |
| US1 — Dashboard | 2 |
| US2 — Account Management | 3 |
| US3 — Balance Snapshots | 2 |
| US4 — CSV Import (core) | 3 |
| US5 — Preview & Confirm | 3 |
| US6 — Error Handling | 3 |
| Polish | 3 |
| **Parallelizable [P] tasks** | 14 |
