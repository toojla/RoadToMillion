using Azure.Core;
using Azure.Identity;
using Npgsql;

namespace RoadToMillion.Api.Configuration;

public static class ServiceCollectionExtensions
{
    public static IHostApplicationBuilder AddDatabase(this IHostApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("roadtomilliondb")
            ?? throw new InvalidOperationException("Connection string 'roadtomilliondb' not found.");

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);

        // In production the connection string has no password — use Azure AD token provider.
        // Locally (Development) Aspire injects a password via service discovery, so no token needed.
        if (builder.Environment.IsProduction())
        {
            dataSourceBuilder.UsePeriodicPasswordProvider(
                async (_, ct) =>
                {
                    var token = await new DefaultAzureCredential().GetTokenAsync(
                        new TokenRequestContext(["https://ossrdbms-aad.database.windows.net/.default"]), ct);
                    return token.Token;
                },
                successRefreshInterval: TimeSpan.FromHours(1),
                failureRefreshInterval: TimeSpan.FromSeconds(10));
        }

        var dataSource = dataSourceBuilder.Build();
        builder.Services.AddSingleton(dataSource);
        builder.Services.AddDbContext<AppDbContext>((sp, options) =>
            options.UseNpgsql(sp.GetRequiredService<NpgsqlDataSource>()));
        builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>();

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

    public static IServiceCollection AddCorsPolicy(this IServiceCollection services)
    {
        services.AddCors(options =>
            options.AddDefaultPolicy(policy =>
                policy.WithOrigins("https://localhost:7200")
                    .AllowAnyMethod()
                    .AllowAnyHeader()));
        return services;
    }
}
