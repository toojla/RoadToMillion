using Microsoft.EntityFrameworkCore;
using RoadToMillion.Api.Data;
using RoadToMillion.Api.Models;

namespace RoadToMillion.Api.Endpoints;

public static class AccountEndpoints
{
    public static void MapAccountEndpoints(this WebApplication app)
    {
        app.MapGet("/api/account-groups/{groupId:int}/accounts", async (int groupId, AppDbContext db) =>
        {
            var groupExists = await db.AccountGroups.AnyAsync(g => g.Id == groupId);
            if (!groupExists) return Results.NotFound();

            var accounts = await db.Accounts
                .Where(a => a.AccountGroupId == groupId)
                .Include(a => a.BalanceSnapshots)
                .ToListAsync();

            var result = accounts.Select(a =>
            {
                var latest = a.BalanceSnapshots
                    .OrderByDescending(s => s.Date)
                    .ThenByDescending(s => s.RecordedAt)
                    .FirstOrDefault();
                return new AccountResponse(
                    a.Id, a.Name, a.Description,
                    latest?.Amount ?? 0m,
                    a.BalanceSnapshots.Any());
            });

            return Results.Ok(result);
        });

        app.MapPost("/api/account-groups/{groupId:int}/accounts", async (int groupId, CreateAccountRequest req, AppDbContext db) =>
        {
            var group = await db.AccountGroups.FindAsync(groupId);
            if (group is null) return Results.NotFound();

            if (string.IsNullOrWhiteSpace(req.Name))
                return Results.BadRequest(new { errors = new { name = new[] { "Name is required." } } });

            var exists = await db.Accounts
                .AnyAsync(a => a.AccountGroupId == groupId && a.Name.ToLower() == req.Name.ToLower());
            if (exists)
                return Results.Conflict(new { errors = new { name = new[] { "An account with this name already exists in this group." } } });

            var account = new Account
            {
                AccountGroupId = groupId,
                Name = req.Name.Trim(),
                Description = req.Description?.Trim()
            };
            db.Accounts.Add(account);
            await db.SaveChangesAsync();

            return Results.Created($"/api/accounts/{account.Id}",
                new AccountResponse(account.Id, account.Name, account.Description, 0m, false));
        });

        app.MapDelete("/api/accounts/{id:int}", async (int id, AppDbContext db) =>
        {
            var account = await db.Accounts.FindAsync(id);
            if (account is null) return Results.NotFound();

            db.Accounts.Remove(account);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        app.MapGet("/api/accounts/{id:int}", async (int id, AppDbContext db) =>
        {
            var account = await db.Accounts
                .Include(a => a.BalanceSnapshots)
                .FirstOrDefaultAsync(a => a.Id == id);
            if (account is null) return Results.NotFound();

            var latest = account.BalanceSnapshots
                .OrderByDescending(s => s.Date)
                .ThenByDescending(s => s.RecordedAt)
                .FirstOrDefault();
            return Results.Ok(new AccountResponse(
                account.Id, account.Name, account.Description,
                latest?.Amount ?? 0m,
                account.BalanceSnapshots.Any()));
        });
    }
}

public record CreateAccountRequest(string Name, string? Description);
