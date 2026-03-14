namespace RoadToMillion.Web.Models;

public record GroupSummary(int Id, string Name, decimal CurrentTotal);

public record PortfolioSummary(
    decimal CurrentTotal,
    decimal GoalAmount,
    decimal RemainingAmount,
    decimal ProgressPercentage,
    List<GroupSummary> Groups);

public record AccountGroupResponse(int Id, string Name, decimal CurrentTotal);

public record AccountResponse(int Id, string Name, string? Description, decimal CurrentBalance, bool HasSnapshots);

public record SnapshotResponse(int Id, decimal Amount, DateOnly Date, DateTime RecordedAt, bool IsMostRecent);

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
    int RowsSkipped)
{
    // Ensure Groups is never null for JSON deserialization
    public List<ImportGroupPreview> Groups { get; init; } = Groups ?? new List<ImportGroupPreview>();
    public List<ImportWarning> Warnings { get; init; } = Warnings ?? new List<ImportWarning>();
}

public record ImportResult(
    int GroupsCreated,
    int AccountsCreated,
    int SnapshotsCreated,
    int RowsSkipped,
    List<ImportWarning> SkipReasons,
    DateTime ImportedAt);
