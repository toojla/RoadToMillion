using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace RoadToMillion.Api.Services;

public class AuthService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IConfiguration configuration) : IAuthService
{
    public async Task<Result<LoginResponse>> LoginAsync(string email, string password)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user == null)
            return Result<LoginResponse>.BadRequest("Invalid email or password.");

        var result = await signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: false);
        if (!result.Succeeded)
            return Result<LoginResponse>.BadRequest("Invalid email or password.");

        var token = GenerateJwtToken(user);
        var response = new LoginResponse(token, user.Email!, user.FirstName, user.LastName);

        return Result<LoginResponse>.Success(response);
    }

    public async Task<Result<RegisterResponse>> RegisterAsync(string email, string password, string? firstName, string? lastName)
    {
        var existingUser = await userManager.FindByEmailAsync(email);
        if (existingUser != null)
            return Result<RegisterResponse>.Conflict("User with this email already exists.");

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FirstName = firstName,
            LastName = lastName
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            return Result<RegisterResponse>.BadRequest(errors);
        }

        var response = new RegisterResponse(user.Id, user.Email!);
        return Result<RegisterResponse>.Created(response, $"/api/users/{user.Id}");
    }

    public async Task<Result> LogoutAsync(string userId)
    {
        // For JWT, logout is typically handled client-side by removing the token
        // You could implement token blacklisting here if needed
        await Task.CompletedTask;
        return Result.Success();
    }

    private string GenerateJwtToken(ApplicationUser user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Email, user.Email!),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Name, user.Email!)
        };

        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            audience: configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(double.Parse(configuration["Jwt:ExpirationInMinutes"]!)),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}