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
