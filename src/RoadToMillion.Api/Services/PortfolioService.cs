namespace RoadToMillion.Api.Services;

public class PortfolioService(AppDbContext db) : IPortfolioService
{
    private const decimal Goal = 1_000_000m;

    public async Task<PortfolioSummary> GetPortfolioSummaryAsync()
    {
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
        var remaining = Math.Max(0m, Goal - currentTotal);
        var progress = Goal == 0 ? 0m : Math.Min(100m, currentTotal / Goal * 100m);

        return new PortfolioSummary(currentTotal, Goal, remaining, progress, groupSummaries);
    }
}
