using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;

namespace RoadToMillion.Api.Configuration;

public static class ServiceCollectionExtensions
{
    public static IHostApplicationBuilder AddDatabase(this IHostApplicationBuilder builder)
    {
        builder.AddNpgsqlDbContext<AppDbContext>("roadtomilliondb");
        return builder;
    }

    extension(IServiceCollection services)
    {
        public IServiceCollection AddAuthenticationServices(IConfiguration configuration)
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

                    // Lockout settings
                    options.Lockout.MaxFailedAccessAttempts = 5;
                    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                    options.Lockout.AllowedForNewUsers = true;

                    // User settings
                    options.User.RequireUniqueEmail = true;
                })
                .AddEntityFrameworkStores<AppDbContext>()
                .AddDefaultTokenProviders();

            // Add JWT Authentication
            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
                })
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = configuration["Jwt:Issuer"],
                        ValidAudience = configuration["Jwt:Audience"],
                        IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                            System.Text.Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!))
                    };
                });

            services.AddAuthorization();

            return services;
        }

        public IServiceCollection AddApplicationServices()
        {
            services.AddScoped<ICsvImportService, CsvImportService>();
            services.AddScoped<IPortfolioService, PortfolioService>();
            services.AddScoped<IAccountGroupService, AccountGroupService>();
            services.AddScoped<IAccountService, AccountService>();
            services.AddScoped<ISnapshotService, SnapshotService>();
            services.AddScoped<IImportService, ImportService>();
            services.AddScoped<IAuthService, AuthService>();
            return services;
        }

        public IServiceCollection AddRateLimitingPolicies()
        {
            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.AddFixedWindowLimiter("auth", limiterOptions =>
                {
                    limiterOptions.PermitLimit = 10;
                    limiterOptions.Window = TimeSpan.FromMinutes(1);
                    limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                    limiterOptions.QueueLimit = 0;
                });
            });
            return services;
        }

        public IServiceCollection AddCorsPolicy(IConfiguration configuration)
        {
            // Reads AllowedOrigins from config; falls back to localhost for local dev.
            // In production, set AllowedOrigins__0 app setting to the Static Web App URL.
            var configured = configuration.GetSection("AllowedOrigins").Get<string[]>()
                ?.Where(o => !string.IsNullOrWhiteSpace(o)).ToArray();
            var origins = configured?.Length > 0 ? configured : ["https://localhost:7200"];

            services.AddCors(options =>
                options.AddDefaultPolicy(policy =>
                    policy.WithOrigins(origins)
                        .AllowAnyMethod()
                        .AllowAnyHeader()));
            return services;
        }
    }
}