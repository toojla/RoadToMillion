using Microsoft.EntityFrameworkCore;
using RoadToMillion.Api.Data;
using RoadToMillion.Api.Models;

namespace RoadToMillion.Api.Endpoints;

public static class SnapshotEndpoints
{
    public static void MapSnapshotEndpoints(this WebApplication app)
    {
        app.MapGet("/api/accounts/{accountId:int}/snapshots", async (int accountId, AppDbContext db) =>
        {
            var accountExists = await db.Accounts.AnyAsync(a => a.Id == accountId);
            if (!accountExists) return Results.NotFound();

            var snapshots = await db.BalanceSnapshots
                .Where(s => s.AccountId == accountId)
                .OrderByDescending(s => s.Date)
                .ThenByDescending(s => s.RecordedAt)
                .ToListAsync();

            var mostRecentId = snapshots.FirstOrDefault()?.Id;
            var result = snapshots.Select(s =>
                new SnapshotResponse(s.Id, s.Amount, s.Date, s.RecordedAt, s.Id == mostRecentId));

            return Results.Ok(result);
        });

        app.MapPost("/api/accounts/{accountId:int}/snapshots", async (int accountId, CreateSnapshotRequest req, AppDbContext db) =>
        {
            var account = await db.Accounts.FindAsync(accountId);
            if (account is null) return Results.NotFound();

            if (req.Amount <= 0)
                return Results.BadRequest(new { errors = new { amount = new[] { "Amount must be greater than zero." } } });

            var snapshot = new BalanceSnapshot
            {
                AccountId = accountId,
                Amount = req.Amount,
                Date = req.Date,
                RecordedAt = DateTime.UtcNow
            };
            db.BalanceSnapshots.Add(snapshot);
            await db.SaveChangesAsync();

            // Determine if this is now the most recent
            var isMostRecent = !await db.BalanceSnapshots
                .Where(s => s.AccountId == accountId && s.Id != snapshot.Id)
                .AnyAsync(s => s.Date > snapshot.Date ||
                               (s.Date == snapshot.Date && s.RecordedAt > snapshot.RecordedAt));

            return Results.Created($"/api/accounts/{accountId}/snapshots/{snapshot.Id}",
                new SnapshotResponse(snapshot.Id, snapshot.Amount, snapshot.Date, snapshot.RecordedAt, isMostRecent));
        });
    }
}

public record CreateSnapshotRequest(decimal Amount, DateOnly Date);
