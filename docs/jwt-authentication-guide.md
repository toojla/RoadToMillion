# JWT Authentication Implementation Guide

## Overview
This guide explains how to add JWT (JSON Web Token) authentication to protect your API and enable secure calls from the Blazor WebAssembly frontend.

## Architecture

```
┌─────────────────────┐         ┌──────────────────────┐
│  Blazor WASM        │         │   API Server         │
│  (Frontend)         │         │   (Backend)          │
│                     │         │                      │
│  1. Login Request   ├────────▶│  /api/auth/login     │
│     (username/pwd)  │         │                      │
│                     │◀────────┤  2. Returns JWT      │
│  3. Store Token     │         │                      │
│     (localStorage)  │         │                      │
│                     │         │                      │
│  4. API Calls with  ├────────▶│  5. Validate Token   │
│     Authorization:  │         │     [Authorize]      │
│     Bearer {token}  │         │                      │
│                     │◀────────┤  6. Protected Data   │
└─────────────────────┘         └──────────────────────┘
```

## Implementation Steps

### Step 1: Add NuGet Packages to API

```bash
cd src/RoadToMillion.Api
dotnet add package Microsoft.AspNetCore.Identity.EntityFrameworkCore
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer
dotnet add package System.IdentityModel.Tokens.Jwt
```

### Step 2: Create ApplicationUser Model

Create `Models/ApplicationUser.cs`:

```csharp
using Microsoft.AspNetCore.Identity;

namespace RoadToMillion.Api.Models;

public class ApplicationUser : IdentityUser
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
}
```

### Step 3: Update AppDbContext

Modify `Data/AppDbContext.cs`:

```csharp
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RoadToMillion.Api.Models;

namespace RoadToMillion.Api.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<AccountGroup> AccountGroups => Set<AccountGroup>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<BalanceSnapshot> BalanceSnapshots => Set<BalanceSnapshot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder); // Important for Identity!

        // Your existing model configuration...
        modelBuilder.Entity<AccountGroup>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.HasIndex(e => e.Name).IsUnique();
            // ... rest of configuration
        });
        
        // ... other configurations
    }
}
```

### Step 4: Add JWT Settings to appsettings.json

```json
{
  "Jwt": {
    "Key": "YourSuperSecretKeyThatIsAtLeast32CharactersLong!",
    "Issuer": "RoadToMillion.Api",
    "Audience": "RoadToMillion.Web",
    "ExpirationInMinutes": 60
  }
}
```

**⚠️ IMPORTANT:** In production, use Azure Key Vault or environment variables for the secret key!

### Step 5: Create Authentication Service

Create `Services/IAuthService.cs`:

```csharp
namespace RoadToMillion.Api.Services;

public interface IAuthService
{
    Task<Result<LoginResponse>> LoginAsync(string email, string password);
    Task<Result<RegisterResponse>> RegisterAsync(string email, string password, string? firstName, string? lastName);
    Task<Result> LogoutAsync(string userId);
}

public record LoginResponse(string Token, string Email, string? FirstName, string? LastName);
public record RegisterResponse(string UserId, string Email);
```

Create `Services/AuthService.cs`:

```csharp
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

        var response = new RegisterResponse(user.Id, user.Email);
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
```

### Step 6: Create Auth Endpoints

Create `Endpoints/AuthEndpoints.cs`:

```csharp
namespace RoadToMillion.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var auth = app.MapGroup("/api/auth");

        auth.MapPost("/register", async (IAuthService authService, RegisterRequest request) =>
        {
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
    }
}

public record LoginRequest(string Email, string Password);
public record RegisterRequest(string Email, string Password, string? FirstName, string? LastName);
```

### Step 7: Update ServiceCollectionExtensions

Add to `Configuration/ServiceCollectionExtensions.cs`:

```csharp
public static IServiceCollection AddAuthentication(this IServiceCollection services, IConfiguration configuration)
{
    // Add Identity
    services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        // Password settings
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredLength = 8;

        // User settings
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

    // Add JWT Authentication
    services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = configuration["Jwt:Issuer"],
            ValidAudience = configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!))
        };
    });

    services.AddAuthorization();
    
    return services;
}

public static IServiceCollection AddApplicationServices(this IServiceCollection services)
{
    services.AddScoped<ICsvImportService, CsvImportService>();
    services.AddScoped<IPortfolioService, PortfolioService>();
    services.AddScoped<IAccountGroupService, AccountGroupService>();
    services.AddScoped<IAccountService, AccountService>();
    services.AddScoped<ISnapshotService, SnapshotService>();
    services.AddScoped<IImportService, ImportService>();
    services.AddScoped<IAuthService, AuthService>(); // Add this
    return services;
}
```

### Step 8: Update Program.cs

```csharp
// Add authentication services
builder.Services.AddAuthentication(builder.Configuration);

// ... existing code ...

var app = builder.Build();

// ... existing code ...

// Add these BEFORE endpoint mappings
app.UseAuthentication();
app.UseAuthorization();

// Register endpoints
app.MapAuthEndpoints(); // Add this
app.MapPortfolioEndpoints();
// ... other endpoints
```

### Step 9: Protect Your Endpoints

Add `[Authorize]` attribute or `.RequireAuthorization()`:

**Option 1: Using attributes** (for controllers):
```csharp
[Authorize]
public class MyController : ControllerBase { }
```

**Option 2: For minimal APIs**:
```csharp
app.MapGet("/api/portfolio/summary", async (IPortfolioService portfolioService) =>
{
    var summary = await portfolioService.GetPortfolioSummaryAsync();
    return Results.Ok(summary);
}).RequireAuthorization(); // Add this!
```

Or protect all endpoints in a group:
```csharp
var protectedGroup = app.MapGroup("/api").RequireAuthorization();
protectedGroup.MapGet("/portfolio/summary", async (IPortfolioService portfolioService) => { ... });
```

### Step 10: Add Migration

```bash
cd src/RoadToMillion.Api
dotnet ef migrations add AddIdentity
dotnet ef database update
```

---

## Frontend Implementation (Blazor WASM)

### Step 1: Create Auth Models

Create `Models/LoginRequest.cs`:
```csharp
namespace RoadToMillion.Web.Models;

public record LoginRequest(string Email, string Password);
public record RegisterRequest(string Email, string Password, string? FirstName, string? LastName);
public record LoginResponse(string Token, string Email, string? FirstName, string? LastName);
public record RegisterResponse(string UserId, string Email);
```

### Step 2: Create AuthService

Create `Services/AuthService.cs`:
```csharp
using System.Net.Http.Json;
using System.Net.Http.Headers;
using Microsoft.JSInterop;

namespace RoadToMillion.Web.Services;

public class AuthService
{
    private readonly HttpClient _httpClient;
    private readonly IJSRuntime _jsRuntime;
    private string? _token;

    public AuthService(HttpClient httpClient, IJSRuntime jsRuntime)
    {
        _httpClient = httpClient;
        _jsRuntime = jsRuntime;
    }

    public async Task<LoginResponse?> LoginAsync(string email, string password)
    {
        var request = new LoginRequest(email, password);
        var response = await _httpClient.PostAsJsonAsync("/api/auth/login", request);

        if (!response.IsSuccessStatusCode)
            return null;

        var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
        if (loginResponse != null)
        {
            await SetTokenAsync(loginResponse.Token);
        }

        return loginResponse;
    }

    public async Task<RegisterResponse?> RegisterAsync(string email, string password, string? firstName, string? lastName)
    {
        var request = new RegisterRequest(email, password, firstName, lastName);
        var response = await _httpClient.PostAsJsonAsync("/api/auth/register", request);

        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<RegisterResponse>();
    }

    public async Task LogoutAsync()
    {
        await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "authToken");
        _token = null;
        _httpClient.DefaultRequestHeaders.Authorization = null;
    }

    public async Task<string?> GetTokenAsync()
    {
        if (_token == null)
        {
            _token = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "authToken");
        }
        return _token;
    }

    private async Task SetTokenAsync(string token)
    {
        _token = token;
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "authToken", token);
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public async Task InitializeAsync()
    {
        var token = await GetTokenAsync();
        if (!string.IsNullOrEmpty(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }
}
```

### Step 3: Register AuthService in Program.cs

```csharp
builder.Services.AddScoped<AuthService>();
```

### Step 4: Initialize Auth on App Start

In `App.razor` or `MainLayout.razor`:
```csharp
@inject AuthService Auth

@code {
    protected override async Task OnInitializedAsync()
    {
        await Auth.InitializeAsync();
    }
}
```

### Step 5: Create Login Page

Create `Pages/Login.razor`:
```razor
@page "/login"
@inject AuthService Auth
@inject NavigationManager Navigation

<h3>Login</h3>

@if (!string.IsNullOrEmpty(errorMessage))
{
    <div class="alert alert-danger">@errorMessage</div>
}

<div class="card" style="max-width: 400px;">
    <div class="card-body">
        <div class="mb-3">
            <label class="form-label">Email</label>
            <input @bind="email" type="email" class="form-control" />
        </div>
        <div class="mb-3">
            <label class="form-label">Password</label>
            <input @bind="password" type="password" class="form-control" />
        </div>
        <button @onclick="HandleLogin" class="btn btn-primary" disabled="@isLoading">
            @if (isLoading)
            {
                <span>Logging in...</span>
            }
            else
            {
                <span>Login</span>
            }
        </button>
    </div>
</div>

@code {
    private string email = "";
    private string password = "";
    private string? errorMessage;
    private bool isLoading;

    private async Task HandleLogin()
    {
        errorMessage = null;
        isLoading = true;

        try
        {
            var result = await Auth.LoginAsync(email, password);
            if (result != null)
            {
                Navigation.NavigateTo("/");
            }
            else
            {
                errorMessage = "Invalid email or password.";
            }
        }
        catch (Exception ex)
        {
            errorMessage = $"Login failed: {ex.Message}";
        }
        finally
        {
            isLoading = false;
        }
    }
}
```

---

## Security Best Practices

### ✅ DO:
- Use HTTPS in production
- Store JWT secret key in environment variables or Azure Key Vault
- Set appropriate token expiration times
- Implement refresh tokens for long-lived sessions
- Validate tokens on every request
- Use strong password requirements
- Implement rate limiting on login endpoint

### ❌ DON'T:
- Store JWT in cookies (XSS vulnerable in WASM)
- Use localStorage for sensitive data other than tokens
- Hardcode secret keys
- Use weak encryption algorithms
- Ignore token expiration

---

## Alternative: Use Azure AD B2C

For a simpler approach without managing users yourself:

1. Create Azure AD B2C tenant
2. Add `Microsoft.AspNetCore.Authentication.JwtBearer` to API
3. Add `Microsoft.Authentication.WebAssembly.Msal` to Blazor
4. Configure API to validate Azure AD tokens
5. Configure Blazor to use MSAL for authentication

**Benefits:**
- No password management
- MFA support
- Social logins (Google, Facebook, etc.)
- Password reset flows

---

## Testing

### Test Registration:
```bash
curl -X POST https://localhost:7100/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"Test123!","firstName":"John","lastName":"Doe"}'
```

### Test Login:
```bash
curl -X POST https://localhost:7100/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"Test123!"}'
```

### Test Protected Endpoint:
```bash
curl https://localhost:7100/api/portfolio/summary \
  -H "Authorization: Bearer YOUR_JWT_TOKEN_HERE"
```

---

## Next Steps

1. Implement user-specific data (filter accounts by userId)
2. Add refresh tokens
3. Implement "Remember me" functionality
4. Add password reset flow
5. Add email confirmation
6. Implement role-based authorization
7. Add audit logging

---

## Resources

- [ASP.NET Core Identity](https://learn.microsoft.com/aspnet/core/security/authentication/identity)
- [JWT Authentication](https://jwt.io/introduction)
- [Blazor Authentication](https://learn.microsoft.com/aspnet/core/blazor/security/webassembly)
- [Azure AD B2C](https://learn.microsoft.com/azure/active-directory-b2c/)
