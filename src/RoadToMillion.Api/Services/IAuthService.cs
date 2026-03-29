namespace RoadToMillion.Api.Services;

public interface IAuthService
{
    Task<Result<LoginResponse>> LoginAsync(string email, string password);

    Task<Result<RegisterResponse>> RegisterAsync(string email, string password, string? firstName, string? lastName);

    Task<Result> LogoutAsync(string userId);
}

public record LoginResponse(string Token, string Email, string? FirstName, string? LastName);
public record RegisterResponse(string UserId, string Email);