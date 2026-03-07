# API Contract: Road to Million Tracker

**Branch**: `001-million-tracker` | **Date**: 2026-03-07  
**Base URL (dev)**: `https://localhost:7100`  
**Format**: JSON (`Content-Type: application/json`) unless noted  
**Auth**: None (single-user local app)

All responses use standard HTTP status codes. Error responses follow:

```json
{
  "errors": {
    "fieldName": ["error message"]
  },
  "title": "One or more validation errors occurred.",
  "status": 400
}
```

---

## Portfolio

### GET /api/portfolio/summary

Returns the computed dashboard summary.

**Response 200**:
```json
{
  "currentTotal": 650000.00,
  "goalAmount": 1000000.00,
  "remainingAmount": 350000.00,
  "progressPercentage": 65.0,
  "groups": [
    {
      "id": 1,
      "name": "Avanza",
      "currentTotal": 400000.00
    },
    {
      "id": 2,
      "name": "Nordnet",
      "currentTotal": 250000.00
    }
  ]
}
```

---

## Account Groups

### GET /api/account-groups

Returns all account groups with their current subtotals.

**Response 200**:
```json
[
  {
    "id": 1,
    "name": "Avanza",
    "currentTotal": 400000.00
  }
]
```

---

### POST /api/account-groups

Creates a new account group.

**Request body**:
```json
{
  "name": "Avanza"
}
```

**Validation**:
- `name`: required, non-blank, max 200 chars, case-insensitively unique

**Response 201** with `Location: /api/account-groups/{id}`:
```json
{
  "id": 3,
  "name": "Avanza",
  "currentTotal": 0.00
}
```

**Response 400**: name blank or fails validation  
**Response 409**: group with that name already exists

---

### DELETE /api/account-groups/{id}

Deletes an account group and all its accounts and balance snapshots.

**Response 204**: deleted  
**Response 404**: group not found

---

## Accounts

### GET /api/account-groups/{groupId}/accounts

Returns all accounts within a group with their current balance.

**Response 200**:
```json
[
  {
    "id": 10,
    "name": "ISK",
    "description": "Investeringssparkonto",
    "currentBalance": 400000.00,
    "hasSnapshots": true
  }
]
```

**Response 404**: group not found

---

### POST /api/account-groups/{groupId}/accounts

Creates a new account within the specified group.

**Request body**:
```json
{
  "name": "ISK",
  "description": "Investeringssparkonto"
}
```

**Validation**:
- `name`: required, non-blank, max 200 chars, case-insensitively unique within the group
- `description`: optional, max 500 chars

**Response 201** with `Location: /api/accounts/{id}`:
```json
{
  "id": 10,
  "name": "ISK",
  "description": "Investeringssparkonto",
  "currentBalance": 0.00,
  "hasSnapshots": false
}
```

**Response 400**: validation failure  
**Response 404**: group not found  
**Response 409**: account with that name already exists in the group

---

### DELETE /api/accounts/{id}

Deletes an account and all its balance snapshots.

**Response 204**: deleted  
**Response 404**: account not found

---

## Balance Snapshots

### GET /api/accounts/{accountId}/snapshots

Returns all balance snapshots for an account, ordered by date descending.

**Response 200**:
```json
[
  {
    "id": 100,
    "amount": 400000.00,
    "date": "2026-03-01",
    "recordedAt": "2026-03-01T14:32:00Z",
    "isMostRecent": true
  },
  {
    "id": 98,
    "amount": 380000.00,
    "date": "2026-02-01",
    "recordedAt": "2026-02-01T09:10:00Z",
    "isMostRecent": false
  }
]
```

**Response 404**: account not found

---

### POST /api/accounts/{accountId}/snapshots

Records a new balance snapshot for an account.

**Request body**:
```json
{
  "amount": 400000.00,
  "date": "2026-03-01"
}
```

**Validation**:
- `amount`: required, must be > 0
- `date`: required, ISO 8601 date string (`YYYY-MM-DD`)

**Response 201** with `Location: /api/accounts/{accountId}/snapshots/{id}`:
```json
{
  "id": 101,
  "amount": 400000.00,
  "date": "2026-03-01",
  "recordedAt": "2026-03-07T10:00:00Z",
  "isMostRecent": true
}
```

**Response 400**: validation failure  
**Response 404**: account not found

---

## CSV Import

### POST /api/import/preview

Parses an uploaded CSV file and returns a preview without writing any data. The client sends the file as `multipart/form-data`.

**Request**: `Content-Type: multipart/form-data`

| Field | Type | Description |
|-------|------|-------------|
| `file` | file | The CSV file to parse |

**CSV format**:
- Required columns (case-insensitive): `AccountGroup`, `AccountName`
- Optional column: `Balance` (decimal, any common format)
- Supported delimiters: `,` `;` `\t` (auto-detected)
- First row must be a header row

**Response 200** — preview generated (even if some rows have warnings):
```json
{
  "rowsTotal": 5,
  "rowsValid": 4,
  "rowsSkipped": 1,
  "groups": [
    {
      "name": "Avanza",
      "alreadyExists": false,
      "accounts": [
        {
          "name": "ISK",
          "initialBalance": 400000.00,
          "alreadyExists": false,
          "willBeSkipped": false
        }
      ]
    },
    {
      "name": "Nordnet",
      "alreadyExists": true,
      "accounts": [
        {
          "name": "Aktiedepå",
          "initialBalance": null,
          "alreadyExists": true,
          "willBeSkipped": true
        }
      ]
    }
  ],
  "warnings": [
    {
      "rowNumber": 4,
      "field": "AccountName",
      "message": "AccountName is missing; row will be skipped.",
      "severity": "Warning"
    },
    {
      "rowNumber": null,
      "field": null,
      "message": "Account 'Aktiedepå' already exists in group 'Nordnet' and will be skipped.",
      "severity": "Info"
    }
  ]
}
```

**Response 400** — file cannot be parsed at all:
```json
{
  "errors": {
    "file": ["File type not supported. Please upload a CSV file."]
  },
  "status": 400
}
```

```json
{
  "errors": {
    "file": ["Required columns are missing: AccountGroup, AccountName"]
  },
  "status": 400
}
```

```json
{
  "errors": {
    "file": ["The file is empty or contains no data rows."]
  },
  "status": 400
}
```

---

### POST /api/import/confirm

Saves the previewed data. The client sends back the `ImportPreview` response body received from the preview endpoint. All writes occur in a single database transaction.

**Request body**: the full `ImportPreview` JSON object from the preview response (client echoes it back).

**Response 200** — import completed:
```json
{
  "groupsCreated": 1,
  "accountsCreated": 2,
  "snapshotsCreated": 2,
  "rowsSkipped": 1,
  "skipReasons": [
    {
      "rowNumber": null,
      "field": null,
      "message": "Account 'Aktiedepå' already exists in group 'Nordnet' and was skipped.",
      "severity": "Info"
    }
  ],
  "importedAt": "2026-03-07T10:05:00Z"
}
```

**Response 400**: request body malformed or contains no valid records to import  
**Response 409**: conflict detected server-side (e.g., data changed between preview and confirm); client should re-run preview
