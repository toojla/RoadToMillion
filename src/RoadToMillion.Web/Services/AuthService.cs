using System.Net.Http.Json;
using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Microsoft.JSInterop;
using RoadToMillion.Web.Models;

namespace RoadToMillion.Web.Services;

public class AuthService(HttpClient httpClient, IJSRuntime jsRuntime, IConfiguration configuration)
{
    private string? _token;

    public event Action? OnAuthStateChanged;

    public async Task<LoginResponse?> LoginAsync(string email, string password)
    {
        var request = new LoginRequest(email, password);
        var response = await httpClient.PostAsJsonAsync("/api/auth/login", request);

        if (!response.IsSuccessStatusCode)
            return null;

        var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
        if (loginResponse != null)
        {
            await SetTokenAsync(loginResponse.Token);
            OnAuthStateChanged?.Invoke();
        }

        return loginResponse;
    }

    public async Task<RegisterResponse?> RegisterAsync(string email, string password, string? firstName, string? lastName)
    {
        var request = new RegisterRequest(email, password, firstName, lastName);
        var response = await httpClient.PostAsJsonAsync("/api/auth/register", request);

        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<RegisterResponse>();
    }

    public async Task LogoutAsync()
    {
        await jsRuntime.InvokeVoidAsync("localStorage.removeItem", "authToken");
        _token = null;
        httpClient.DefaultRequestHeaders.Authorization = null;
        OnAuthStateChanged?.Invoke();
    }

    public async Task<string?> GetTokenAsync()
    {
        _token ??= await jsRuntime.InvokeAsync<string?>("localStorage.getItem", "authToken");
        return _token;
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        var token = await GetTokenAsync();
        return !string.IsNullOrEmpty(token);
    }

    public Task<bool> IsRegistrationEnabledAsync()
    {
        var enabled = configuration.GetValue<bool>("Features:EnableUserRegistration", true);
        return Task.FromResult(enabled);
    }

    private async Task SetTokenAsync(string token)
    {
        _token = token;
        await jsRuntime.InvokeVoidAsync("localStorage.setItem", "authToken", token);
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public async Task InitializeAsync()
    {
        var token = await GetTokenAsync();
        if (!string.IsNullOrEmpty(token))
        {
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }
}

public record RegistrationStatusResponse(bool RegistrationEnabled);