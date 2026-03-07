# Data Model: Road to Million Tracker

**Branch**: `001-million-tracker` | **Date**: 2026-03-07  
**Source**: [spec.md](spec.md) | **Research**: [research.md](research.md)

---

## Entities

### AccountGroup

Represents a financial institution or platform (e.g., "Avanza", "Nordnet", "Swedbank").

| Field | Type | Constraints |
|-------|------|-------------|
| `Id` | `int` | Primary key, auto-increment |
| `Name` | `string` | Required, max 200 chars, unique (case-insensitive) |

**Relationships**:
- Has zero or more `Account` (cascade delete: deleting a group deletes all its accounts and their snapshots)

**Derived value**:
- `CurrentTotal` — sum of `Account.CurrentBalance` for all accounts in the group (computed, not stored)

**Validation rules**:
- Name must not be blank or whitespace
- Name must be unique across all account groups (case-insensitive comparison)

---

### Account

Represents a specific financial product within a group (e.g., "ISK", "Aktiedepå", "Sparkonto").

| Field | Type | Constraints |
|-------|------|-------------|
| `Id` | `int` | Primary key, auto-increment |
| `AccountGroupId` | `int` | Foreign key → `AccountGroup.Id`, required |
| `Name` | `string` | Required, max 200 chars |
| `Description` | `string?` | Optional, max 500 chars |

**Relationships**:
- Belongs to exactly one `AccountGroup`
- Has zero or more `BalanceSnapshot` (cascade delete: deleting an account deletes all its snapshots)

**Derived value**:
- `CurrentBalance` — amount from the single most recent `BalanceSnapshot` by `Date`; 0 if no snapshots exist

**Validation rules**:
- Name must not be blank or whitespace
- Name must be unique within its `AccountGroup` (case-insensitive comparison)

---

### BalanceSnapshot

A recorded measurement of an account's monetary value at a specific point in time.

| Field | Type | Constraints |
|-------|------|-------------|
| `Id` | `int` | Primary key, auto-increment |
| `AccountId` | `int` | Foreign key → `Account.Id`, required |
| `Amount` | `decimal` | Required, must be > 0 |
| `Date` | `DateOnly` | Required |
| `RecordedAt` | `DateTime` | UTC timestamp of when the snapshot was saved; used as tiebreaker when two snapshots share the same `Date` |

**Relationships**:
- Belongs to exactly one `Account`

**Validation rules**:
- `Amount` must be greater than zero
- `Date` is user-supplied; it may be any date (past, present, or future — no restriction)
- When two snapshots share the same `Date`, the one with the latest `RecordedAt` is treated as the current balance

---

## Derived / View Models (not persisted)

### PortfolioSummary

Computed on-demand from the current state of the database. Never stored.

| Field | Type | Description |
|-------|------|-------------|
| `CurrentTotal` | `decimal` | Sum of `Account.CurrentBalance` across all groups |
| `GoalAmount` | `decimal` | Fixed: 1,000,000 SEK |
| `RemainingAmount` | `decimal` | `GoalAmount - CurrentTotal`; minimum displayed value: 0 |
| `ProgressPercentage` | `decimal` | `(CurrentTotal / GoalAmount) * 100`; capped at 100 |
| `Groups` | `List<GroupSummary>` | Per-group breakdown |

**GroupSummary** (nested):

| Field | Type | Description |
|-------|------|-------------|
| `Id` | `int` | Account group ID |
| `Name` | `string` | Account group name |
| `CurrentTotal` | `decimal` | Sum of current balances of all accounts in this group |

---

### ImportPreview

Produced by parsing a CSV file. Never stored; returned to the client for confirmation.

| Field | Type | Description |
|-------|------|-------------|
| `Groups` | `List<ImportGroupPreview>` | All groups and accounts detected in the file |
| `Warnings` | `List<ImportWarning>` | Non-fatal issues: duplicates, skipped rows, existing conflicts |
| `RowsTotal` | `int` | Total data rows in the file |
| `RowsValid` | `int` | Rows that will be imported on confirm |
| `RowsSkipped` | `int` | Rows that will be skipped (with reasons in `Warnings`) |

**ImportGroupPreview** (nested):

| Field | Type | Description |
|-------|------|-------------|
| `Name` | `string` | Account group name from CSV |
| `AlreadyExists` | `bool` | True if a group with this name already exists in the system |
| `Accounts` | `List<ImportAccountPreview>` | Accounts to create under this group |

**ImportAccountPreview** (nested):

| Field | Type | Description |
|-------|------|-------------|
| `Name` | `string` | Account name from CSV |
| `InitialBalance` | `decimal?` | Balance from CSV (if column present and parseable); null otherwise |
| `AlreadyExists` | `bool` | True if an account with this name already exists under this group |
| `WillBeSkipped` | `bool` | True if this account will not be imported (e.g., duplicate) |

**ImportWarning** (nested):

| Field | Type | Description |
|-------|------|-------------|
| `RowNumber` | `int?` | 1-based CSV data row number; null for file-level warnings |
| `Field` | `string?` | Which column caused the issue; null for row-level issues |
| `Message` | `string` | Human-readable description of the issue |
| `Severity` | `enum` | `Info`, `Warning` (skipped row) |

---

### ImportResult

Produced after a successful confirmed import. Never stored beyond the HTTP response.

| Field | Type | Description |
|-------|------|-------------|
| `GroupsCreated` | `int` | Number of new account groups created |
| `AccountsCreated` | `int` | Number of new accounts created |
| `SnapshotsCreated` | `int` | Number of balance snapshots recorded (one per imported balance) |
| `RowsSkipped` | `int` | Number of rows skipped |
| `SkipReasons` | `List<ImportWarning>` | Details of each skipped row |
| `ImportedAt` | `DateTime` | UTC timestamp of the import |

---

## Relationships Diagram

```
AccountGroup (1) ──── (0..*) Account (1) ──── (0..*) BalanceSnapshot
```

- Deleting `AccountGroup` cascades to all child `Account` records and their `BalanceSnapshot` records.
- Deleting `Account` cascades to all child `BalanceSnapshot` records.

---

## State Transitions: Import Flow

```
[File Selected]
      │
      ▼
[POST /api/import/preview]
      │
      ├── Parse error / missing columns → Error response (no state change)
      │
      ▼
[ImportPreview returned to client]
      │
      ├── User cancels → No state change
      │
      ▼
[POST /api/import/confirm  (client sends ImportPreview back)]
      │
      ├── Validation failure → Error response (no state change)
      │
      ▼
[AccountGroups + Accounts + BalanceSnapshots written in one transaction]
      │
      ▼
[ImportResult returned to client]
```

Key invariant: **no database write occurs until the confirm step**. If the confirm transaction fails for any reason, the database is unchanged.

---

## Validation Summary

| Entity | Field | Rule |
|--------|-------|------|
| AccountGroup | Name | Required; max 200 chars; unique case-insensitive |
| Account | Name | Required; max 200 chars; unique case-insensitive within group |
| Account | Description | Optional; max 500 chars |
| BalanceSnapshot | Amount | Required; > 0 |
| BalanceSnapshot | Date | Required; any date accepted |
| ImportFile | File type | Must be `.csv` or `text/csv` or `text/plain` |
| ImportFile | Required columns | `AccountGroup` and `AccountName` headers must be present |
| ImportAccountPreview | InitialBalance | Must be > 0 if present; null if column absent or value unparseable |
