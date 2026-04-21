namespace RoadToMillion.Api.Services;

public interface IAccountService
{
    Task<Result<IEnumerable<AccountResponse>>> GetAccountsByGroupAsync(int groupId);

    Task<Result<AccountResponse>> GetAccountByIdAsync(int id);

    Task<Result<AccountResponse>> CreateAccountAsync(int groupId, string name, string? description, AccountType type = AccountType.Regular);

    Task<Result> DeleteAccountAsync(int id);
}