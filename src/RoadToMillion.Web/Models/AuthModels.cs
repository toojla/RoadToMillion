namespace RoadToMillion.Web.Models;

public record LoginRequest(string Email, string Password);
public record RegisterRequest(string Email, string Password, string? FirstName, string? LastName);
public record LoginResponse(string Token, string Email, string? FirstName, string? LastName);
public record RegisterResponse(string UserId, string Email);