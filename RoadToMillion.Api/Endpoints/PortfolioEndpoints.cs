using Microsoft.EntityFrameworkCore;
using RoadToMillion.Api.Data;
using RoadToMillion.Api.Models;

namespace RoadToMillion.Api.Endpoints;

public static class PortfolioEndpoints
{
    public static void MapPortfolioEndpoints(this WebApplication app)
    {
        app.MapGet("/api/portfolio/summary", async (AppDbContext db) =>
        {
            const decimal goal = 1_000_000m;

            var groups = await db.AccountGroups
                .Include(g => g.Accounts)
                    .ThenInclude(a => a.BalanceSnapshots)
                .ToListAsync();

            var groupSummaries = groups.Select(g =>
            {
                var total = g.Accounts.Sum(a =>
                    a.BalanceSnapshots
                        .OrderByDescending(s => s.Date)
                        .ThenByDescending(s => s.RecordedAt)
                        .FirstOrDefault()?.Amount ?? 0m);
                return new GroupSummary(g.Id, g.Name, total);
            }).ToList();

            var currentTotal = groupSummaries.Sum(g => g.CurrentTotal);
            var remaining = Math.Max(0m, goal - currentTotal);
            var progress = goal == 0 ? 0m : Math.Min(100m, currentTotal / goal * 100m);

            return Results.Ok(new PortfolioSummary(currentTotal, goal, remaining, progress, groupSummaries));
        });
    }
}
