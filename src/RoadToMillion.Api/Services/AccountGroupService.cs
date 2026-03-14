namespace RoadToMillion.Api.Services;

public class AccountGroupService(AppDbContext db) : IAccountGroupService
{
    public async Task<IEnumerable<AccountGroupResponse>> GetAllAccountGroupsAsync()
    {
        var groups = await db.AccountGroups
            .Include(g => g.Accounts)
                .ThenInclude(a => a.BalanceSnapshots)
            .ToListAsync();

        return groups.Select(g =>
        {
            var total = g.Accounts.Sum(a =>
                a.BalanceSnapshots
                    .OrderByDescending(s => s.Date)
                    .ThenByDescending(s => s.RecordedAt)
                    .FirstOrDefault()?.Amount ?? 0m);
            return new AccountGroupResponse(g.Id, g.Name, total);
        });
    }

    public async Task<Result<AccountGroupResponse>> CreateAccountGroupAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result<AccountGroupResponse>.BadRequest("Name is required.");

        var exists = await db.AccountGroups
            .AnyAsync(g => g.Name.ToLower() == name.ToLower());

        if (exists)
            return Result<AccountGroupResponse>.Conflict("A group with this name already exists.");

        var group = new AccountGroup { Name = name.Trim() };
        db.AccountGroups.Add(group);
        await db.SaveChangesAsync();

        var response = new AccountGroupResponse(group.Id, group.Name, 0m);
        return Result<AccountGroupResponse>.Created(response, $"/api/account-groups/{group.Id}");
    }

    public async Task<Result> DeleteAccountGroupAsync(int id)
    {
        var group = await db.AccountGroups.FindAsync(id);
        if (group is null)
            return Result.NotFound();

        db.AccountGroups.Remove(group);
        await db.SaveChangesAsync();
        return Result.NoContent();
    }
}