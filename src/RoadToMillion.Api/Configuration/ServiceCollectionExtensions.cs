using RoadToMillion.Api.Data;
using RoadToMillion.Api.Services;

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
        public IServiceCollection AddApplicationServices()
        {
            services.AddScoped<CsvImportService>();
            return services;
        }

        public IServiceCollection AddCorsPolicy()
        {
            services.AddCors(options =>
                options.AddDefaultPolicy(policy =>
                    policy.WithOrigins("https://localhost:7200")
                        .AllowAnyMethod()
                        .AllowAnyHeader()));
            return services;
        }
    }
}