using System.Net.Http.Json;
using RoadToMillion.Web.Models;

namespace RoadToMillion.Web.Services;

public class ApiClient(HttpClient http)
{
    // --- Portfolio ---
    public Task<PortfolioSummary?> GetPortfolioSummaryAsync() =>
        http.GetFromJsonAsync<PortfolioSummary>("api/portfolio/summary");

    // --- Account Groups ---
    public Task<List<AccountGroupResponse>?> GetAccountGroupsAsync() =>
        http.GetFromJsonAsync<List<AccountGroupResponse>>("api/account-groups");

    public async Task<AccountGroupResponse?> CreateAccountGroupAsync(string name)
    {
        var response = await http.PostAsJsonAsync("api/account-groups", new { name });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AccountGroupResponse>();
    }

    public Task DeleteAccountGroupAsync(int id) =>
        http.DeleteAsync($"api/account-groups/{id}");

    // --- Accounts ---
    public Task<List<AccountResponse>?> GetAccountsAsync(int groupId) =>
        http.GetFromJsonAsync<List<AccountResponse>>($"api/account-groups/{groupId}/accounts");

    public async Task<AccountResponse?> CreateAccountAsync(int groupId, string name, string? description)
    {
        var response = await http.PostAsJsonAsync($"api/account-groups/{groupId}/accounts", new { name, description });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AccountResponse>();
    }

    public Task DeleteAccountAsync(int id) =>
        http.DeleteAsync($"api/accounts/{id}");

    public Task<AccountResponse?> GetAccountAsync(int id) =>
        http.GetFromJsonAsync<AccountResponse>($"api/accounts/{id}");

    // --- Snapshots ---
    public Task<List<SnapshotResponse>?> GetSnapshotsAsync(int accountId) =>
        http.GetFromJsonAsync<List<SnapshotResponse>>($"api/accounts/{accountId}/snapshots");

    public async Task<SnapshotResponse?> CreateSnapshotAsync(int accountId, decimal amount, DateOnly date)
    {
        var response = await http.PostAsJsonAsync($"api/accounts/{accountId}/snapshots", new { amount, date });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SnapshotResponse>();
    }

    // --- Import ---
    public async Task<(ImportPreview? Preview, string? Error)> GetImportPreviewAsync(MultipartFormDataContent content)
    {
        var response = await http.PostAsync("api/import/preview", content);
        if (response.IsSuccessStatusCode)
            return (await response.Content.ReadFromJsonAsync<ImportPreview>(), null);
        var error = await response.Content.ReadAsStringAsync();
        return (null, error);
    }

    public async Task<(ImportResult? Result, string? Error)> ConfirmImportAsync(ImportPreview preview)
    {
        var response = await http.PostAsJsonAsync("api/import/confirm", preview);
        if (response.IsSuccessStatusCode)
            return (await response.Content.ReadFromJsonAsync<ImportResult>(), null);
        var error = await response.Content.ReadAsStringAsync();
        return (null, error);
    }
}
