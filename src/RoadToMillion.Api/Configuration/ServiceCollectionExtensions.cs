namespace RoadToMillion.Api.Configuration;

public static class ServiceCollectionExtensions
{
    public static IHostApplicationBuilder AddDatabase(this IHostApplicationBuilder builder)
    {
        builder.AddNpgsqlDbContext<AppDbContext>("roadtomilliondb");
        return builder;
    }

    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<ICsvImportService, CsvImportService>();
        services.AddScoped<IPortfolioService, PortfolioService>();
        services.AddScoped<IAccountGroupService, AccountGroupService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<ISnapshotService, SnapshotService>();
        services.AddScoped<IImportService, ImportService>();
        return services;
    }

    public static IServiceCollection AddCorsPolicy(this IServiceCollection services, IConfiguration configuration)
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
