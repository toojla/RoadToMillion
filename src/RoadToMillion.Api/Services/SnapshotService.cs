namespace RoadToMillion.Api.Services;

public class SnapshotService(AppDbContext db) : ISnapshotService
{
    public async Task<Result<IEnumerable<SnapshotResponse>>> GetSnapshotsByAccountAsync(int accountId)
    {
        var accountExists = await db.Accounts.AnyAsync(a => a.Id == accountId);
        if (!accountExists)
            return Result<IEnumerable<SnapshotResponse>>.NotFound();

        var snapshots = await db.BalanceSnapshots
            .Where(s => s.AccountId == accountId)
            .OrderByDescending(s => s.Date)
            .ThenByDescending(s => s.RecordedAt)
            .ToListAsync();

        var mostRecentId = snapshots.FirstOrDefault()?.Id;
        var result = snapshots.Select(s =>
            new SnapshotResponse(s.Id, s.Amount, s.Date, s.RecordedAt, s.Id == mostRecentId));

        return Result<IEnumerable<SnapshotResponse>>.Success(result);
    }

    public async Task<Result<SnapshotResponse>> CreateSnapshotAsync(int accountId, decimal amount, DateOnly date)
    {
        var account = await db.Accounts.FindAsync(accountId);
        if (account is null)
            return Result<SnapshotResponse>.NotFound();

        if (amount <= 0)
            return Result<SnapshotResponse>.BadRequest("Amount must be greater than zero.");

        var snapshot = new BalanceSnapshot
        {
            AccountId = accountId,
            Amount = amount,
            Date = date,
            RecordedAt = DateTime.UtcNow
        };
        db.BalanceSnapshots.Add(snapshot);
        await db.SaveChangesAsync();

        // Determine if this is now the most recent
        var isMostRecent = !await db.BalanceSnapshots
            .Where(s => s.AccountId == accountId && s.Id != snapshot.Id)
            .AnyAsync(s => s.Date > snapshot.Date ||
                           (s.Date == snapshot.Date && s.RecordedAt > snapshot.RecordedAt));

        var response = new SnapshotResponse(snapshot.Id, snapshot.Amount, snapshot.Date, snapshot.RecordedAt, isMostRecent);
        return Result<SnapshotResponse>.Created(response, $"/api/accounts/{accountId}/snapshots/{snapshot.Id}");
    }
}
