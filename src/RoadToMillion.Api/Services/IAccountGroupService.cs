namespace RoadToMillion.Api.Services;

public interface IAccountGroupService
{
    Task<IEnumerable<AccountGroupResponse>> GetAllAccountGroupsAsync();

    Task<Result<AccountGroupResponse>> CreateAccountGroupAsync(string name);

    Task<Result> DeleteAccountGroupAsync(int id);
}