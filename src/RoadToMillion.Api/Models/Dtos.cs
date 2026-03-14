namespace RoadToMillion.Api.Models;

// --- Portfolio ---

public record GroupSummary(int Id, string Name, decimal CurrentTotal);

public record PortfolioSummary(
    decimal CurrentTotal,
    decimal GoalAmount,
    decimal RemainingAmount,
    decimal ProgressPercentage,
    IEnumerable<GroupSummary> Groups);

// --- Account Group responses ---

public record AccountGroupResponse(int Id, string Name, decimal CurrentTotal);

// --- Account responses ---

public record AccountResponse(int Id, string Name, string? Description, decimal CurrentBalance, bool HasSnapshots);

// --- Snapshot responses ---

public record SnapshotResponse(int Id, decimal Amount, DateOnly Date, DateTime RecordedAt, bool IsMostRecent);

// --- Import ---

public record ImportWarning(int? RowNumber, string? Field, string Message, string Severity);

public record ImportSnapshotPreview(decimal Amount, DateTime? SnapshotDate);

public record ImportAccountPreview(string Name, bool AlreadyExists, List<ImportSnapshotPreview> Snapshots)
{
    // Ensure Snapshots is never null for JSON deserialization
    public List<ImportSnapshotPreview> Snapshots { get; init; } = Snapshots ?? new List<ImportSnapshotPreview>();
}

public record ImportGroupPreview(string Name, bool AlreadyExists, List<ImportAccountPreview> Accounts)
{
    // Ensure Accounts is never null for JSON deserialization
    public List<ImportAccountPreview> Accounts { get; init; } = Accounts ?? new List<ImportAccountPreview>();
}

public record ImportPreview(
    List<ImportGroupPreview> Groups,
    List<ImportWarning> Warnings,
    int RowsTotal,
    int RowsValid,
    int RowsSkipped);

public record ImportResult(
    int GroupsCreated,
    int AccountsCreated,
    int SnapshotsCreated,
    int RowsSkipped,
    List<ImportWarning> SkipReasons,
    DateTime ImportedAt);
