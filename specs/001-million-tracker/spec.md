# Feature Specification: Road to Million Tracker

**Feature Branch**: `001-million-tracker`  
**Created**: 2026-03-05  
**Updated**: 2026-03-07  
**Status**: Draft  
**Input**: User description: "Track progress to one million SEK across multiple accounts with a .NET 10 API (project: RoadToMillion.Api), EF Core SQLite backend, and Blazor WebAssembly frontend; orchestrated via .NET Aspire; import account information from a previous system via CSV"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - View Progress Dashboard (Priority: P1)

As a user tracking my wealth, I want to open the app and immediately see my total current portfolio value across all accounts and groups, a visual progress bar toward 1,000,000 SEK, the remaining amount I need to save, and a breakdown of how much each account group contributes — so I can stay motivated and informed at a glance.

**Why this priority**: This is the core purpose of the app. Without a meaningful summary view, every other feature lacks context. A user who only ever opens the dashboard and never edits anything still gets value.

**Independent Test**: Can be tested by opening the app with pre-seeded account group and account data and verifying that the total value, per-group subtotals, remaining amount, and progress indicator all display correctly. Delivers standalone value as a read-only status view.

**Acceptance Scenarios**:

1. **Given** the user has account groups with accounts that have recorded values, **When** they open the app, **Then** they see the sum of the most recent balance snapshot per account as the "Current Total", the amount remaining to 1,000,000 SEK, and a progress bar reflecting the percentage achieved.
2. **Given** account groups exist, **When** the user views the dashboard, **Then** each group displays its own subtotal (sum of current balances of all accounts within it).
3. **Given** the total across all accounts exceeds 1,000,000 SEK, **When** the user views the dashboard, **Then** the progress bar shows 100% and the remaining amount shows 0 (or a positive surplus).
4. **Given** no account groups or accounts exist yet, **When** the user opens the app, **Then** the dashboard shows 0 SEK current total, 1,000,000 SEK remaining, and an empty progress bar with a prompt to add an account group.

---

### User Story 2 - Register and Manage Account Groups and Accounts (Priority: P2)

As a user, I want to create account groups representing financial institutions or platforms (e.g., "Avanza", "Nordnet", "Swedbank"), add named accounts within each group (e.g., "ISK", "Aktiedepå", "Sparkonto"), and delete groups or individual accounts I no longer want to track — so the system reflects my actual financial landscape in a structured way.

**Why this priority**: Without account groups and accounts, no balance snapshots can be recorded and the tracker has no content. This hierarchical structure is the foundation, but the dashboard (P1) can be demonstrated with seeded data before this story is built.

**Independent Test**: Can be tested by creating a new account group, adding an account to it, verifying both appear in the management view, then deleting the account and verifying it is removed. Delivers value by enabling the user to configure what they want to track.

**Acceptance Scenarios**:

1. **Given** the user is on the account groups page, **When** they submit a new group with a name, **Then** the group appears in the list immediately.
2. **Given** an account group exists, **When** the user adds a new account to it with a name and optional description, **Then** the account appears under that group.
3. **Given** an account has been created, **When** the user views the group's account list, **Then** they see the account name, optional description, and its most recent recorded balance (or "No snapshots yet" if none).
4. **Given** an account exists, **When** the user deletes it, **Then** the account and all its associated balance snapshots are removed and no longer contribute to the total.
5. **Given** an account group exists, **When** the user deletes the group, **Then** the group and all its accounts (and their snapshots) are removed.
6. **Given** the user submits a group or account with a blank name, **When** the form is submitted, **Then** an error message is shown and nothing is created.

---

### User Story 3 - Record Balance Snapshots Over Time (Priority: P3)

As a user, I want to open an account and record its current balance on any given date — so the system has up-to-date data to include in the overall progress calculation, and I can see how each account's value has changed over time.

**Why this priority**: Recording balance snapshots is what keeps the data fresh, but the app has value even with static initial snapshots. This story builds on Story 2 and adds the time-series dimension.

**Independent Test**: Can be tested by navigating to an account detail view, entering a monetary amount and date, submitting it, and verifying that the new snapshot appears in the history list and that the dashboard total updates to reflect the new balance.

**Acceptance Scenarios**:

1. **Given** the user is viewing an account detail page, **When** they submit a new balance snapshot with an amount and a date, **Then** the snapshot appears at the top of the history list for that account ordered by date descending.
2. **Given** an account has multiple balance snapshots, **When** the dashboard total is calculated, **Then** only the most recent snapshot by date is used to represent that account's current balance.
3. **Given** the user submits a balance snapshot with a negative or zero amount, **When** the form is submitted, **Then** an error message is shown and the snapshot is not saved.
4. **Given** an account has no balance snapshots, **When** the dashboard is displayed, **Then** that account contributes 0 SEK to its group's subtotal and to the overall total.

---

### User Story 4 - Import Accounts from CSV File (Priority: P4)

As a user migrating from a previous system, I want to upload a CSV file containing my account and account group information so that all my existing accounts are imported into the tracker without having to enter them one by one.

**Why this priority**: This is a one-time migration convenience. The app functions fully without it, but it removes significant manual data-entry burden for users coming from another tool.

**Independent Test**: Can be tested by uploading a valid CSV file and verifying that the resulting account groups and accounts appear in the account management view with the correct names and groupings. Delivers standalone value as a one-time migration action.

**Acceptance Scenarios**:

1. **Given** the user has a valid CSV file with account group and account information, **When** they select the file and confirm the import, **Then** all account groups and accounts described in the file are created and visible in the account management view.
2. **Given** a CSV file with multiple rows belonging to the same account group name, **When** the import is processed, **Then** all those accounts are grouped under a single account group with that name (no duplicate groups are created).
3. **Given** a well-formed CSV file is uploaded, **When** the import completes, **Then** the user receives a success message showing how many account groups and accounts were created.
4. **Given** the user has no existing account groups in the system, **When** a CSV import is completed successfully, **Then** the dashboard reflects the newly imported accounts with their initial balances (if provided) or zero balances.

---

### User Story 5 - Preview and Confirm Before Import (Priority: P5)

As a user, I want to see a preview of the accounts that will be imported from my CSV file before committing to the import, so that I can verify the data looks correct and avoid creating unwanted or duplicate records.

**Why this priority**: Importing data is a potentially hard-to-undo action. Providing a preview step greatly reduces user errors and builds trust in the import process.

**Independent Test**: Can be tested independently by uploading a CSV file and verifying that a preview table is shown listing all detected account groups and accounts before any data is written. Can be cancelled without any changes taking effect.

**Acceptance Scenarios**:

1. **Given** the user selects a valid CSV file, **When** the file is parsed, **Then** a preview is shown listing all account groups and accounts detected, before any data is saved.
2. **Given** the preview is displayed, **When** the user cancels, **Then** no data is written and the system remains unchanged.
3. **Given** the preview is displayed, **When** the user confirms the import, **Then** the data is saved and the user is taken to the account management view.
4. **Given** the CSV contains an account group that already exists in the system, **When** the preview is shown, **Then** the user is informed which groups or accounts already exist and will be skipped, so they can review before choosing to confirm (existing records are skipped, new ones created) or cancel.

---

### User Story 6 - Handle Invalid or Malformed CSV Files (Priority: P6)

As a user, I want to receive clear feedback when my CSV file cannot be imported due to formatting or data problems, so that I understand what went wrong and can correct the file before trying again.

**Why this priority**: Error handling and feedback are essential for a good user experience, but the feature still delivers value without perfect error messages. This story improves quality and reduces frustration.

**Independent Test**: Can be tested by uploading files with known errors (missing required columns, empty file, non-CSV file) and verifying that appropriate error messages are shown and no data is written.

**Acceptance Scenarios**:

1. **Given** the user uploads a file that is not a valid CSV (e.g., a PDF or image), **When** the system attempts to parse it, **Then** an error message is shown explaining the file type is not supported and no data is written.
2. **Given** the user uploads a CSV file that is missing required columns (e.g., account name), **When** the file is parsed, **Then** an error message is shown identifying which required columns are missing and no data is written.
3. **Given** the user uploads a CSV file that is empty or contains only headers with no data rows, **When** parsed, **Then** a message informs the user that no accounts were found to import.
4. **Given** the CSV contains some rows with missing required field values, **When** the file is parsed, **Then** the system identifies the problematic rows and informs the user which rows could not be imported, while still allowing valid rows to proceed.

---

### Edge Cases

- What happens when two balance snapshots for the same account share the same date? The system picks one consistently (e.g., the one recorded most recently in the system).
- How does the system handle an account being deleted that has balance snapshots? All associated snapshots are removed along with the account.
- How does the system handle an account group being deleted? All accounts within the group, and all their balance snapshots, are removed.
- What if the user records a balance for a past date that is earlier than existing snapshots? The existing most-recent snapshot remains the one used in the total; the historic snapshot is stored but does not affect the current balance calculation.
- What if the total portfolio value is zero? The progress bar shows 0% and the remaining amount shows 1,000,000 SEK.
- What if an account group has no accounts? The group displays a subtotal of 0 SEK and contributes nothing to the overall total.
- What happens when a CSV file contains an account group name that already exists in the system? The system detects the conflict and informs the user in the preview step; the user decides whether to merge (add accounts to the existing group) or skip.
- What happens if the CSV file is extremely large (hundreds of rows)? The system processes it without timing out and shows a progress indicator if processing takes more than a few seconds.
- What happens if the CSV uses a delimiter other than a comma (e.g., semicolons or tabs)? The system attempts to auto-detect common delimiters; if detection fails, an informative error is shown.
- What if an account name already exists within an account group being imported? The system flags the duplicate in the preview and skips creating it to avoid duplicate accounts.
- What if the user navigates away mid-import? The import either completes or is fully rolled back; no partial data is left in the system.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST allow users to create account groups with a required name.
- **FR-002**: System MUST allow users to delete an account group, including all of its accounts and their balance snapshots.
- **FR-003**: System MUST allow users to create accounts within an account group, with a required name and optional description.
- **FR-004**: System MUST allow users to delete individual accounts, including all their associated balance snapshots.
- **FR-005**: System MUST allow users to record a balance snapshot (amount in SEK and a date) for any account.
- **FR-006**: System MUST display a list of all account groups, each showing its name and current subtotal (sum of most recent balance snapshot per account within it).
- **FR-007**: System MUST display all accounts within a group, each showing its name, optional description, and most recent recorded balance (or "No snapshots yet" if none).
- **FR-008**: System MUST display a chronological balance snapshot history for each individual account.
- **FR-009**: System MUST calculate the current portfolio total as the sum of the single most recent balance snapshot per account, across all groups.
- **FR-010**: System MUST calculate and display how much remains to reach the goal of 1,000,000 SEK.
- **FR-011**: System MUST display a visual progress indicator showing the percentage of the goal achieved.
- **FR-012**: System MUST display per-group subtotals alongside the overall portfolio total on the dashboard.
- **FR-013**: System MUST persist all account group, account, and balance snapshot data so it is available across sessions.
- **FR-014**: System MUST reject creation of an account group or account with a blank name and inform the user with an error message.
- **FR-015**: System MUST reject a balance snapshot with an amount of zero or below and inform the user with an error message.
- **FR-016**: System MUST expose all data operations through an API so the frontend and any future client can consume them independently.
- **FR-017**: System MUST allow users to upload a CSV file containing account information for import.
- **FR-018**: System MUST parse the CSV file and display a preview of all account groups and accounts detected before saving any data.
- **FR-019**: System MUST allow the user to confirm or cancel the import from the preview screen without writing any data until confirmation.
- **FR-020**: System MUST create account groups from the imported data, grouping accounts with the same group name under one group (case-insensitive match).
- **FR-021**: System MUST create accounts within the appropriate account groups based on the CSV data.
- **FR-022**: System MUST detect and report account group names that already exist in the system and display them in the preview.
- **FR-023**: System MUST detect and skip duplicate account names within the same group during import, reporting each skip to the user.
- **FR-024**: System MUST reject files that are not in CSV format and display a clear, actionable error message.
- **FR-025**: System MUST validate that required columns (at minimum: account name and account group name) are present in the uploaded file and report missing columns clearly.
- **FR-026**: System MUST inform the user of rows that cannot be imported due to missing required field values, and allow valid rows to still be imported.
- **FR-027**: System MUST display a success summary after a completed import showing the number of account groups and accounts created.
- **FR-028**: System MUST import initial balance values per account if the CSV includes a balance column, recording them as balance snapshots dated the day of import.
- **FR-029**: System MUST treat the import as an atomic operation — either all valid records are saved together, or the import is fully cancelled, with no partial state left in the system.
- **FR-030**: System MUST support CSV files using common delimiters: comma, semicolon, and tab, with auto-detection.

### Key Entities

- **AccountGroup**: Represents a financial institution or platform (e.g., "Avanza", "Nordnet"). Has a unique identifier and a required name. An account group contains zero or more accounts. Its current value is the sum of the current balances of all accounts within it.
- **Account**: Represents a specific asset or sub-account within a group (e.g., "ISK", "Sparkonto"). Has a unique identifier, a required name, an optional description, and a reference to its parent account group. An account can have zero or more balance snapshots. Its current balance is the amount from its most recent snapshot by date.
- **BalanceSnapshot**: A recorded measurement of an account's monetary value at a specific point in time. Has a unique identifier, a reference to its parent account, a positive amount in SEK, and a date. Multiple snapshots can exist per account; only the most recent by date is considered the account's current balance.
- **Portfolio Summary**: A derived, read-only view computed from the most recent balance snapshot per account, across all groups. Contains the current total value, a per-group breakdown of subtotals, the goal amount (1,000,000 SEK), the remaining amount, and the progress percentage.
- **ImportFile**: The uploaded file submitted by the user. Has a name, file type, and raw content. Must be a CSV format with at minimum a header row and one data row.
- **ImportPreview**: A derived, read-only view generated from parsing the CSV file before committing. Contains a list of account groups to be created, accounts under each group, any initial balances, and any detected warnings (duplicates, missing fields, conflicts with existing data).
- **ImportResult**: A summary produced after a successful import. Contains counts of account groups created, accounts created, rows skipped (with reasons), and the timestamp of the import.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can view their current total portfolio value and progress toward the goal within 2 seconds of the app loading, under normal network conditions.
- **SC-002**: Users can add a new account and have it appear in the account list in under 60 seconds from start to finish.
- **SC-003**: Users can record a new entry for an account and see the dashboard total update to reflect it in under 30 seconds.
- **SC-004**: The portfolio total always reflects the most recent entry per account; no stale values are used in the calculation.
- **SC-005**: All data entered by the user (accounts and entries) remains available the next time the app is opened.
- **SC-006**: The app functions correctly when the portfolio spans between 0 and at least 50 accounts with multiple entries each.
- **SC-007**: Users can complete the full import flow — from file selection to confirmed import — in under 2 minutes for files with up to 100 accounts.
- **SC-008**: A CSV file with up to 500 account rows is parsed and previewed in under 5 seconds.
- **SC-009**: 100% of accounts in a valid, well-formed CSV file are imported correctly without data loss or corruption.
- **SC-010**: Users receive a clear, actionable error message for every category of invalid file (wrong file type, missing columns, empty file) so they can correct and retry without additional support.
- **SC-011**: After a successful import, all imported accounts appear in the account management view and contribute to the portfolio dashboard total within the same session, without requiring a manual refresh.
- **SC-012**: No partial data is ever written to the system when an import is cancelled or fails — the system state before and after a failed or cancelled import is identical.

## Assumptions

- The app is for a single user with no authentication or multi-user support required.
- All monetary values are in Swedish Kronor (SEK); no currency conversion is needed.
- The goal amount of 1,000,000 SEK is fixed and does not need to be configurable by the user in the initial version.
- "Most recent balance" is determined by the date field on the balance snapshot, not by the order snapshots were created in the database.
- Account groups represent financial institutions or platforms; accounts within a group represent specific products (ISK, depot, savings account, etc.). No further nesting below accounts is required.
- The frontend communicates with the backend exclusively through the API; there is no direct database access from the client.
- The backend API is a dedicated project named `RoadToMillion.Api` within the solution.
- .NET Aspire is used to orchestrate all runnable components — the API project, the Blazor frontend, and the database — as a single local development environment. No manual per-project startup is required.
- The minimum required columns in an import CSV are: `AccountGroup` (account group name) and `AccountName` (account name), matched case-insensitively. An optional `Balance` column may be present; all other columns are ignored.
- Initial balances imported via CSV, if present, are recorded as balance snapshots dated the day of the import. The import date is the **UTC calendar date** at the time of the confirm operation (consistent with the `RecordedAt` UTC timestamp).
- The import feature is a one-time or occasional migration tool; there is no requirement to schedule or automate recurring imports.
- The import does not overwrite or modify existing accounts or balance snapshots; it only creates new records.
