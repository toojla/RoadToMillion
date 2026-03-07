# Feature Specification: CSV Account Import

**Feature Branch**: `001-csv-account-import`  
**Created**: 2026-03-07  
**Status**: Draft  
**Input**: User description: "Need to add a feature to import csv file with account information from a previous system"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Import Accounts from CSV File (Priority: P1)

As a user migrating from a previous system, I want to upload a CSV file containing my account and account group information so that all my existing accounts are imported into the tracker without having to enter them one by one.

**Why this priority**: This is the core of the feature. Without the ability to upload and parse a CSV file, nothing else in this feature has meaning. A user who only ever uses this story gets immediate value: their existing accounts are in the system.

**Independent Test**: Can be tested by uploading a valid CSV file and verifying that the resulting account groups and accounts appear in the account management view with the correct names and groupings. Delivers standalone value as a one-time migration action.

**Acceptance Scenarios**:

1. **Given** the user has a valid CSV file with account group and account information, **When** they select the file and confirm the import, **Then** all account groups and accounts described in the file are created and visible in the account management view.
2. **Given** a CSV file with multiple rows belonging to the same account group name, **When** the import is processed, **Then** all those accounts are grouped under a single account group with that name (no duplicate groups are created).
3. **Given** a well-formed CSV file is uploaded, **When** the import completes, **Then** the user receives a success message showing how many account groups and accounts were created.
4. **Given** the user has no existing account groups in the system, **When** a CSV import is completed successfully, **Then** the dashboard reflects the newly imported accounts with their initial balances (if provided) or zero balances.

---

### User Story 2 - Preview and Confirm Before Import (Priority: P2)

As a user, I want to see a preview of the accounts that will be imported from my CSV file before committing to the import, so that I can verify the data looks correct and avoid creating unwanted or duplicate records.

**Why this priority**: Importing data is a potentially hard-to-undo action. Providing a preview step greatly reduces user errors and builds trust in the import process.

**Independent Test**: Can be tested independently by uploading a CSV file and verifying that a preview table is shown listing all detected account groups and accounts before any data is written. Can be cancelled without any changes taking effect.

**Acceptance Scenarios**:

1. **Given** the user selects a valid CSV file, **When** the file is parsed, **Then** a preview is shown listing all account groups and accounts detected, before any data is saved.
2. **Given** the preview is displayed, **When** the user cancels, **Then** no data is written and the system remains unchanged.
3. **Given** the preview is displayed, **When** the user confirms the import, **Then** the data is saved and the user is taken to the account management view.
4. **Given** the CSV contains an account group that already exists in the system, **When** the preview is shown, **Then** the user is informed which groups or accounts already exist so they can decide how to proceed.

---

### User Story 3 - Handle Invalid or Malformed CSV Files (Priority: P3)

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

- What happens when a CSV file contains an account group name that already exists in the system? The system detects the conflict and informs the user in the preview step; the user decides whether to merge (add accounts to the existing group) or skip.
- What happens if the CSV file is extremely large (hundreds of rows)? The system processes it without timing out and shows a progress indicator if processing takes more than a few seconds.
- What happens if the CSV uses a different delimiter (semicolons instead of commas)? The system attempts to auto-detect common delimiters (comma, semicolon, tab); if detection fails, an informative error is shown.
- What if an account name already exists within an account group being imported? The system flags the duplicate in the preview and skips creating it to avoid duplicate accounts.
- What if the user navigates away mid-import? The import either completes or is fully rolled back; no partial data is left in the system.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST allow users to upload a CSV file containing account information for import.
- **FR-002**: System MUST parse the CSV file and display a preview of all account groups and accounts detected before saving any data.
- **FR-003**: System MUST allow the user to confirm or cancel the import from the preview screen without writing any data until confirmation.
- **FR-004**: System MUST create account groups from the imported data, grouping accounts with the same group name under one group (case-insensitive match).
- **FR-005**: System MUST create accounts within the appropriate account groups based on the CSV data.
- **FR-006**: System MUST detect and report account group names that already exist in the system and display them in the preview.
- **FR-007**: System MUST detect and skip duplicate account names within the same group during import, reporting each skip to the user.
- **FR-008**: System MUST reject files that are not in CSV format and display a clear, actionable error message.
- **FR-009**: System MUST validate that required columns (at minimum: account name and account group name) are present in the uploaded file and report missing columns clearly.
- **FR-010**: System MUST inform the user of rows that cannot be imported due to missing required field values, and allow valid rows to still be imported.
- **FR-011**: System MUST display a success summary after a completed import showing the number of account groups and accounts created.
- **FR-012**: System MUST import initial balance values per account if the CSV includes a balance column, recording them as balance snapshots dated the day of import.
- **FR-013**: System MUST treat the import as an atomic operation — either all valid records are saved together, or the import is fully cancelled, with no partial state left in the system.
- **FR-014**: System MUST support CSV files using common delimiters: comma, semicolon, and tab, with auto-detection.

### Key Entities

- **ImportFile**: The uploaded file submitted by the user. Has a name, file type, and raw content. Must be a CSV format with at minimum a header row and one data row.
- **ImportPreview**: A derived, read-only view generated from parsing the CSV file before committing. Contains a list of account groups to be created, accounts under each group, any initial balances, and any detected warnings (duplicates, missing fields, conflicts with existing data).
- **ImportResult**: A summary produced after a successful import. Contains counts of account groups created, accounts created, rows skipped (with reasons), and the timestamp of the import.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can complete the full import flow — from file selection to confirmed import — in under 2 minutes for files with up to 100 accounts.
- **SC-002**: A CSV file with up to 500 account rows is parsed and previewed in under 5 seconds.
- **SC-003**: 100% of accounts in a valid, well-formed CSV file are imported correctly without data loss or corruption.
- **SC-004**: Users receive a clear, actionable error message for every category of invalid file (wrong file type, missing columns, empty file) so they can correct and retry without additional support.
- **SC-005**: After a successful import, all imported accounts appear in the account management view and contribute to the portfolio dashboard total within the same session, without requiring a manual refresh.
- **SC-006**: No partial data is ever written to the system when an import is cancelled or fails — the system state before and after a failed or cancelled import is identical.

## Assumptions

- The CSV file originates from a previous account-tracking system used by the same single user; no authentication or authorization beyond accessing the app is required for the import feature.
- The minimum required columns in the CSV are: account group name and account name. An optional balance column may be present; all other columns are ignored.
- Initial balances imported via CSV, if present, are recorded as balance snapshots dated the day of the import.
- Duplicate detection for account group names is case-insensitive (e.g., "Avanza" and "avanza" are treated as the same group).
- The import feature is a one-time or occasional migration tool; there is no requirement to schedule or automate recurring imports.
- All monetary values in the CSV are in SEK; no currency conversion or multi-currency support is required.
- The import does not overwrite or modify existing accounts or balance snapshots; it only creates new records.
