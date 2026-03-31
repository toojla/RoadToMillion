using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace RoadToMillion.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var auth = app.MapGroup("/api/auth")
            .RequireRateLimiting("auth");

        auth.MapPost("/register", async (IAuthService authService, IConfiguration configuration, RegisterRequest request) =>
        {
            if (!ValidateRequestLengths(request.Email, request.Password, request.FirstName, request.LastName))
                return Results.BadRequest(new { errors = new { message = new[] { "Invalid input length." } } });

            // Check if registration is enabled
            var registrationEnabled = configuration.GetValue<bool>("Features:EnableUserRegistration", true);
            if (!registrationEnabled)
            {
                return Results.Problem(
                    detail: "User registration is currently disabled.",
                    statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await authService.RegisterAsync(
                request.Email,
                request.Password,
                request.FirstName,
                request.LastName);

            return result.Type switch
            {
                ResultType.Created => Results.Created(result.Location!, result.Data),
                ResultType.BadRequest => Results.BadRequest(new { errors = new { message = new[] { result.ErrorMessage } } }),
                ResultType.Conflict => Results.Conflict(new { errors = new { email = new[] { result.ErrorMessage } } }),
                _ => Results.Problem("An unexpected error occurred", statusCode: 500)
            };
        }).AllowAnonymous();

        auth.MapPost("/login", async (IAuthService authService, LoginRequest request) =>
        {
            if (!ValidateRequestLengths(request.Email, request.Password))
                return Results.BadRequest(new { errors = new { message = new[] { "Invalid input length." } } });

            var result = await authService.LoginAsync(request.Email, request.Password);

            return result.Type switch
            {
                ResultType.Success => Results.Ok(result.Data),
                ResultType.BadRequest => Results.BadRequest(new { errors = new { message = new[] { result.ErrorMessage } } }),
                _ => Results.Problem("An unexpected error occurred", statusCode: 500)
            };
        }).AllowAnonymous();

        auth.MapPost("/logout", async (IAuthService authService, HttpContext context) =>
        {
            var jti = context.User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti)?.Value;
            var expClaim = context.User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Exp)?.Value;

            if (jti == null || expClaim == null)
                return Results.Unauthorized();

            var expiration = DateTimeOffset.FromUnixTimeSeconds(long.Parse(expClaim));
            await authService.LogoutAsync(jti, expiration);
            return Results.Ok();
        }).RequireAuthorization();

        // Endpoint to check if registration is enabled (for frontend)
        auth.MapGet("/registration-status", (IConfiguration configuration) =>
        {
            var enabled = configuration.GetValue<bool>("Features:EnableUserRegistration", true);
            return Results.Ok(new { registrationEnabled = enabled });
        }).AllowAnonymous();
    }

    private static bool ValidateRequestLengths(string email, string password, string? firstName = null, string? lastName = null)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > 256) return false;
        if (string.IsNullOrWhiteSpace(password) || password.Length > 128) return false;
        if (firstName?.Length > 100) return false;
        if (lastName?.Length > 100) return false;
        return true;
    }
}

public record LoginRequest(
    [property: Required, MaxLength(256)] string Email,
    [property: Required, MaxLength(128)] string Password);

public record RegisterRequest(
    [property: Required, MaxLength(256)] string Email,
    [property: Required, MaxLength(128)] string Password,
    [property: MaxLength(100)] string? FirstName,
    [property: MaxLength(100)] string? LastName);