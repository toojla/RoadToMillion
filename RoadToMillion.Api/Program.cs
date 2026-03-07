using Microsoft.EntityFrameworkCore;
using RoadToMillion.Api.Data;
using RoadToMillion.Api.Endpoints;
using RoadToMillion.Api.Services;
using RoadToMillion.ServiceDefaults;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// EF Core + PostgreSQL
builder.AddNpgsqlDbContext<AppDbContext>("roadtomilliondb");

// CORS – allow the Blazor WASM origin
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins("https://localhost:7200")
              .AllowAnyMethod()
              .AllowAnyHeader()));

// Services
builder.Services.AddScoped<CsvImportService>();

// OpenAPI / Swagger
builder.Services.AddOpenApi();

var app = builder.Build();

app.MapDefaultEndpoints();

// Apply EF migrations on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.UseCors();

// Register endpoint groups
app.MapPortfolioEndpoints();
app.MapAccountGroupEndpoints();
app.MapAccountEndpoints();
app.MapSnapshotEndpoints();
app.MapImportEndpoints();

app.Run();
