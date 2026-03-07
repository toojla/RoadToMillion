using Microsoft.EntityFrameworkCore;
using RoadToMillion.Api.Data;
using RoadToMillion.Api.Models;

namespace RoadToMillion.Api.Endpoints;

public static class AccountGroupEndpoints
{
    public static void MapAccountGroupEndpoints(this WebApplication app)
    {
        app.MapGet("/api/account-groups", async (AppDbContext db) =>
        {
            var groups = await db.AccountGroups
                .Include(g => g.Accounts)
                    .ThenInclude(a => a.BalanceSnapshots)
                .ToListAsync();

            var result = groups.Select(g =>
            {
                var total = g.Accounts.Sum(a =>
                    a.BalanceSnapshots
                        .OrderByDescending(s => s.Date)
                        .ThenByDescending(s => s.RecordedAt)
                        .FirstOrDefault()?.Amount ?? 0m);
                return new AccountGroupResponse(g.Id, g.Name, total);
            });

            return Results.Ok(result);
        });

        app.MapPost("/api/account-groups", async (AppDbContext db, CreateAccountGroupRequest req) =>
        {
            if (string.IsNullOrWhiteSpace(req.Name))
                return Results.BadRequest(new { errors = new { name = new[] { "Name is required." } } });

            var exists = await db.AccountGroups
                .AnyAsync(g => g.Name.ToLower() == req.Name.ToLower());
            if (exists)
                return Results.Conflict(new { errors = new { name = new[] { "A group with this name already exists." } } });

            var group = new AccountGroup { Name = req.Name.Trim() };
            db.AccountGroups.Add(group);
            await db.SaveChangesAsync();

            return Results.Created($"/api/account-groups/{group.Id}",
                new AccountGroupResponse(group.Id, group.Name, 0m));
        });

        app.MapDelete("/api/account-groups/{id:int}", async (int id, AppDbContext db) =>
        {
            var group = await db.AccountGroups.FindAsync(id);
            if (group is null) return Results.NotFound();

            db.AccountGroups.Remove(group);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }
}

public record CreateAccountGroupRequest(string Name);
