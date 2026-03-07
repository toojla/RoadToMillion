# Research: CSV Parsing for .NET 10 ASP.NET Core Web API

**Feature**: CSV Account Import  
**Date**: 2026-03-07  
**Status**: Complete

---

## Decision

**Use CsvHelper (NuGet, `CsvHelper` v33.x).**

For this use case — three columns, up to 500 rows, files from unknown legacy systems, with a preview/validation workflow — CsvHelper is the right tool. It correctly handles all real-world CSV edge cases (RFC 4180 quoted fields, embedded newlines, escaped quotes), maps directly to typed records, and integrates cleanly with ASP.NET Core's `IFormFile`. It is the de-facto standard in the .NET ecosystem, meaning any future maintainer will immediately recognise it.

---

## Rationale

The deciding factors in this domain are:

| Concern | Weight | Notes |
|---|---|---|
| Correctness with unknown input | **Critical** | Files from "previous systems" (Excel, LibreOffice, bank exports) routinely have quoted fields containing commas or newlines. A naive split will silently produce wrong data. |
| Per-row error collection | High | CsvHelper exposes `BadDataException`, missing field handling, and `GetField<T?>()` for nullable types — all usable in a row-by-row loop. |
| Typed mapping | High | Direct `class`/`record` mapping with `ClassMap` eliminates manual index wrangling. |
| Streaming from `IFormFile` | High | `IFormFile.OpenReadStream()` feeds directly into `CsvReader` with no intermediate buffering. |
| Performance | **Irrelevant** | 500 rows ≈ microseconds on any parser. Performance is not a constraint here. |
| Dependency footprint | Low | CsvHelper is a single, well-maintained NuGet package (~50 M lifetime downloads, active releases through .NET 10). |

---

## Alternatives Considered

### 1. `System.IO` + Manual Split — ❌ Rejected

```csharp
// Looks simple, breaks silently on real CSV
var parts = line.Split(',');
```

**Why rejected**: A CSV field containing a comma _must_ be quoted per RFC 4180. Files exported from Excel, LibreOffice, or any bank will include quoted fields. `string.Split` has no awareness of quoting context and will produce corrupt data without any error. For user-supplied files from "previous systems" this failure mode is unacceptable. The simplicity is illusory — you'd eventually need to handle quoting, and at that point you've reimplemented a CSV parser badly.

---

### 2. `Microsoft.VisualBasic.FileIO.TextFieldParser` — ❌ Rejected

```csharp
using var parser = new TextFieldParser(stream);
parser.TextFieldType = FieldType.Delimited;
parser.SetDelimiters(",");
```

**Why rejected**: It _works_, but carries several problems for a modern .NET 10 API:

- Requires `<PackageReference Include="Microsoft.VisualBasic" />` — an odd namespace for a C# project that signals "legacy".
- **No async support** — blocks the thread during I/O, which conflicts with ASP.NET Core's async-first model.
- No built-in typed mapping; everything is `string[]` arrays requiring manual index tracking.
- No community convention or typed error model.
- `SetDelimiters` accepts multiple delimiters simultaneously but treats any of them as valid — it cannot detect _which_ delimiter a file uses consistently.

It is a viable last-resort option when NuGet is unavailable, but that is not the case here.

---

### 3. Sylvan.Data.Csv — ❌ Rejected (but noted for future scale)

```csharp
var reader = CsvDataReader.Create(stream);
while (await reader.ReadAsync()) { ... }
```

**Why rejected**: Sylvan is the fastest .NET CSV parser in benchmarks and a serious option, but the `DbDataReader` / columnar API is more ergonomic for ETL/pipeline scenarios than for a simple three-column mapping task with per-row validation. Its documentation and community are smaller than CsvHelper's, adding friction for future maintainers. The performance advantage is irrelevant for 500 rows.

**Note for future**: If this feature ever needs to process hundreds of thousands of rows or is on a hot path, Sylvan should be re-evaluated.

---

### 4. CsvHelper — ✅ Selected

```csharp
using var reader = new CsvReader(new StreamReader(formFile.OpenReadStream()), CultureInfo.InvariantCulture);
```

Key capabilities used in this feature:

- **Typed class mapping**: Map rows directly to `AccountImportRow` record/class.
- **Header validation**: `reader.HeaderRecord` is available after the first read; check for required column names before iterating rows.
- **Missing/nullable fields**: `reader.GetField<decimal?>()` returns null for an empty Balance cell without throwing.
- **Custom configuration**: Pass a `CsvConfiguration` for delimiter, `MissingFieldFound = null` (suppress throws for optional columns), `HeaderValidated = null` (do your own validation).
- **Per-row error collection**: Wrap each `reader.GetRecord<T>()` in a try/catch or use `TryGetField` to accumulate `RowError` objects without aborting the whole parse.

---

## Auto-Detecting the Delimiter

There is no magic heuristic, but a simple scoring approach on the first line (or first 3 lines for robustness) is sufficient for the three supported delimiters:

```csharp
internal static char DetectDelimiter(Stream stream)
{
    // Read only the first line; do NOT consume the stream
    using var peekReader = new StreamReader(stream, leaveOpen: true);
    var firstLine = peekReader.ReadLine() ?? string.Empty;
    stream.Position = 0; // reset for the real parser

    var candidates = new[] { ',', ';', '\t' };

    // Count raw occurrences (outside quotes) per candidate
    return candidates
        .OrderByDescending(c => CountOutsideQuotes(firstLine, c))
        .First();
}

private static int CountOutsideQuotes(string line, char delimiter)
{
    int count = 0;
    bool inQuotes = false;
    foreach (char ch in line)
    {
        if (ch == '"') inQuotes = !inQuotes;
        else if (!inQuotes && ch == delimiter) count++;
    }
    return count;
}
```

**Rules**:
- The delimiter with the highest count wins. Ties default to `,`.
- If all counts are 0, the file is either a single-column file or not a CSV at all — return a validation error.
- Run detection _before_ constructing `CsvReader`; pass the detected char into `CsvConfiguration.Delimiter`.

**Edge case**: A file where the first row has quoted content containing the non-delimiter character. The `CountOutsideQuotes` logic above handles this correctly by skipping characters inside `"..."`.

---

## Header Validation

CsvHelper reads headers lazily. The cleanest pattern is:

```csharp
// 1. Read the header row explicitly
await reader.ReadAsync();
reader.ReadHeader();

// 2. Validate required columns (case-insensitive)
var headers = reader.HeaderRecord!
    .Select(h => h.Trim())
    .ToHashSet(StringComparer.OrdinalIgnoreCase);

var requiredColumns = new[] { "AccountGroup", "AccountName" };
var missing = requiredColumns.Where(col => !headers.Contains(col)).ToList();

if (missing.Count > 0)
{
    // Return early — do NOT read any data rows
    return ParseResult.Failure($"Missing required columns: {string.Join(", ", missing)}");
}

// 3. Warn about unrecognised columns (informational, not an error)
var knownColumns = new[] { "AccountGroup", "AccountName", "Balance" };
var unknown = headers.Except(knownColumns, StringComparer.OrdinalIgnoreCase).ToList();
// surface these as warnings in the ImportPreview if desired
```

This approach:
- Validates _before_ processing any data rows, so no partial state is built.
- Is case-insensitive, tolerating `accountgroup`, `ACCOUNTGROUP`, etc.
- Correctly handles extra/unknown columns (ignore them rather than reject the file).
- Reports all missing columns in one error, not just the first.

---

## Per-Row Error Collection Pattern

```csharp
var rows = new List<AccountImportRow>();
var errors = new List<RowError>();
int rowNumber = 1; // 1-based, header is row 0

while (await reader.ReadAsync())
{
    rowNumber++;
    var accountGroup = reader.GetField<string>("AccountGroup")?.Trim();
    var accountName  = reader.GetField<string>("AccountName")?.Trim();
    var balanceRaw   = reader.GetField<string?>("Balance")?.Trim();

    var rowErrors = new List<string>();

    if (string.IsNullOrEmpty(accountGroup))
        rowErrors.Add("AccountGroup is required");

    if (string.IsNullOrEmpty(accountName))
        rowErrors.Add("AccountName is required");

    decimal? balance = null;
    if (!string.IsNullOrEmpty(balanceRaw))
    {
        if (decimal.TryParse(balanceRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            balance = parsed;
        else
            rowErrors.Add($"Balance '{balanceRaw}' is not a valid decimal number");
    }

    if (rowErrors.Count > 0)
        errors.Add(new RowError(rowNumber, rowErrors));
    else
        rows.Add(new AccountImportRow(accountGroup!, accountName!, balance));
}

return new ParseResult(rows, errors);
```

This satisfies **FR-010**: rows with missing required fields are reported individually while valid rows proceed.

---

## .NET 10 Specific Considerations

| Area | Detail |
|---|---|
| **CsvHelper version** | Use **v33.x** (current as of early 2026). It targets `netstandard2.0` and `net6.0+`, so it works on net10.0 without issues. |
| **Native AOT** | CsvHelper uses reflection by default. If the API is ever compiled with Native AOT, switch to `CsvHelper`'s source-generator-based class maps (`CsvHelper.Configuration.Attributes`) or migrate to Sylvan (which is AOT-friendly). Blazor WASM is irrelevant here — this is server-side API code. |
| **`IFormFile` / streaming** | No changes in .NET 10. `IFormFile.OpenReadStream()` returns a non-seekable `Stream` for large uploads. Since files are small (≤ 500 rows), reading into a `MemoryStream` first is acceptable and enables seeking (required if the delimiter-detection step resets `Position`). |
| **`System.Text.Json`** | Not involved in CSV parsing, but the API response (`ImportPreview`, `RowError` etc.) should use STJ source generation (`[JsonSerializable]`) to stay AOT-safe. |
| **`CultureInfo.InvariantCulture`** | Always pass `InvariantCulture` to `CsvConfiguration`. Balance values from a Swedish banking export may use `.` or `,` as decimal separator — handle this explicitly in the `decimal.TryParse` call rather than relying on culture defaults. |
| **Async throughput** | For ≤ 500 rows, synchronous reading is fine. If async is preferred for consistency with the rest of the controller, `CsvReader.ReadAsync()` is available and works normally in net10.0. |

---

## Summary Table

| Library | Correct RFC 4180 | Typed mapping | Header validation | Async | Dependency | Verdict |
|---|---|---|---|---|---|---|
| CsvHelper | ✅ | ✅ Native | Manual (2 lines) | ✅ | 1 NuGet pkg | **✅ Use this** |
| Manual split | ❌ Fails on quotes | ❌ Manual | Manual | ✅ | None | ❌ |
| TextFieldParser | ✅ | ❌ Manual | Manual | ❌ | VB package | ❌ |
| Sylvan.Data.Csv | ✅ | Via `DbDataReader` | Manual | ✅ | 1 NuGet pkg | ⚠️ Overkill |
