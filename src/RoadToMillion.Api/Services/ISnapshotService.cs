namespace RoadToMillion.Api.Services;

public interface ISnapshotService
{
    Task<Result<IEnumerable<SnapshotResponse>>> GetSnapshotsByAccountAsync(int accountId);

    Task<Result<SnapshotResponse>> CreateSnapshotAsync(int accountId, decimal amount, DateOnly date);
}