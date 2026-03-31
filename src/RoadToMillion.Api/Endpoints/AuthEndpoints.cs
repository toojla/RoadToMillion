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
            var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null)
                return Results.Unauthorized();

            await authService.LogoutAsync(userId);
            return Results.Ok();
        }).RequireAuthorization();

        // Endpoint to check if registration is enabled (for frontend)
        auth.MapGet("/registration-status", (IConfiguration configuration) =>
        {
            var enabled = configuration.GetValue<bool>("Features:EnableUserRegistration", true);
            return Results.Ok(new { registrationEnabled = enabled });
        }).AllowAnonymous();
    }
}

public record LoginRequest(string Email, string Password);
public record RegisterRequest(string Email, string Password, string? FirstName, string? LastName);