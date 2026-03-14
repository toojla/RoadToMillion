namespace RoadToMillion.Api.Services;

public class AccountService(AppDbContext db) : IAccountService
{
    public async Task<Result<IEnumerable<AccountResponse>>> GetAccountsByGroupAsync(int groupId)
    {
        var groupExists = await db.AccountGroups.AnyAsync(g => g.Id == groupId);
        if (!groupExists)
            return Result<IEnumerable<AccountResponse>>.NotFound();

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

        return Result<IEnumerable<AccountResponse>>.Success(result);
    }

    public async Task<Result<AccountResponse>> GetAccountByIdAsync(int id)
    {
        var account = await db.Accounts
            .Include(a => a.BalanceSnapshots)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (account is null)
            return Result<AccountResponse>.NotFound();

        var latest = account.BalanceSnapshots
            .OrderByDescending(s => s.Date)
            .ThenByDescending(s => s.RecordedAt)
            .FirstOrDefault();

        var response = new AccountResponse(
            account.Id, account.Name, account.Description,
            latest?.Amount ?? 0m,
            account.BalanceSnapshots.Any());

        return Result<AccountResponse>.Success(response);
    }

    public async Task<Result<AccountResponse>> CreateAccountAsync(int groupId, string name, string? description)
    {
        var group = await db.AccountGroups.FindAsync(groupId);
        if (group is null)
            return Result<AccountResponse>.NotFound();

        if (string.IsNullOrWhiteSpace(name))
            return Result<AccountResponse>.BadRequest("Name is required.");

        var exists = await db.Accounts
            .AnyAsync(a => a.AccountGroupId == groupId && a.Name.ToLower() == name.ToLower());

        if (exists)
            return Result<AccountResponse>.Conflict("An account with this name already exists in this group.");

        var account = new Account
        {
            AccountGroupId = groupId,
            Name = name.Trim(),
            Description = description?.Trim()
        };
        db.Accounts.Add(account);
        await db.SaveChangesAsync();

        var response = new AccountResponse(account.Id, account.Name, account.Description, 0m, false);
        return Result<AccountResponse>.Created(response, $"/api/accounts/{account.Id}");
    }

    public async Task<Result> DeleteAccountAsync(int id)
    {
        var account = await db.Accounts.FindAsync(id);
        if (account is null)
            return Result.NotFound();

        db.Accounts.Remove(account);
        await db.SaveChangesAsync();
        return Result.NoContent();
    }
}