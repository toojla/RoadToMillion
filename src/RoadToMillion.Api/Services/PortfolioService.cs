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

        static decimal BalanceAsOf(Account a, DateOnly date) =>
            a.BalanceSnapshots
                .Where(s => s.Date <= date)
                .OrderByDescending(s => s.Date)
                .ThenByDescending(s => s.RecordedAt)
                .FirstOrDefault()?.Amount ?? 0m;

        static (DateOnly? current, DateOnly? previous) TopTwoDates(IEnumerable<Account> accounts)
        {
            var dates = accounts
                .SelectMany(a => a.BalanceSnapshots.Select(s => s.Date))
                .Distinct()
                .OrderByDescending(d => d)
                .Take(2)
                .ToList();
            return (
                dates.Count >= 1 ? dates[0] : null,
                dates.Count >= 2 ? dates[1] : null);
        }

        var regularAccounts = groups
            .SelectMany(g => g.Accounts)
            .Where(a => a.Type == AccountType.Regular)
            .ToList();

        var pensionAccounts = groups
            .SelectMany(g => g.Accounts)
            .Where(a => a.Type == AccountType.ServicePension)
            .ToList();

        var (regularAsOf, regularPrev) = TopTwoDates(regularAccounts);
        var (pensionAsOf, pensionPrev) = TopTwoDates(pensionAccounts);

        var groupSummaries = groups.Select(g =>
        {
            var total = regularAsOf is { } d
                ? g.Accounts.Where(a => a.Type == AccountType.Regular).Sum(a => BalanceAsOf(a, d))
                : 0m;
            return new GroupSummary(g.Id, g.Name, total);
        }).ToList();

        var currentTotal = groupSummaries.Sum(g => g.CurrentTotal);
        var remaining = Math.Max(0m, Goal - currentTotal);
        var progress = Goal == 0 ? 0m : Math.Min(100m, currentTotal / Goal * 100m);

        decimal? previousTotal = regularPrev is { } rp
            ? regularAccounts.Sum(a => BalanceAsOf(a, rp))
            : null;
        decimal? changeAmount = previousTotal is { } pt ? currentTotal - pt : null;
        decimal? changePercentage = previousTotal is { } pt2 && pt2 != 0m
            ? (currentTotal - pt2) / pt2 * 100m
            : null;

        var pensionTotal = pensionAsOf is { } pd
            ? pensionAccounts.Sum(a => BalanceAsOf(a, pd))
            : 0m;
        decimal? previousPensionTotal = pensionPrev is { } pp
            ? pensionAccounts.Sum(a => BalanceAsOf(a, pp))
            : null;
        decimal? pensionChangeAmount = previousPensionTotal is { } ppt ? pensionTotal - ppt : null;
        decimal? pensionChangePercentage = previousPensionTotal is { } ppt2 && ppt2 != 0m
            ? (pensionTotal - ppt2) / ppt2 * 100m
            : null;

        return new PortfolioSummary(
            currentTotal,
            Goal,
            remaining,
            progress,
            pensionTotal,
            groupSummaries,
            regularAsOf,
            regularPrev,
            previousTotal,
            changeAmount,
            changePercentage,
            pensionAsOf,
            pensionPrev,
            previousPensionTotal,
            pensionChangeAmount,
            pensionChangePercentage);
    }
}
