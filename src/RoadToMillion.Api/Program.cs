using RoadToMillion.Api.Configuration;
using RoadToMillion.Api.Endpoints;
using RoadToMillion.ServiceDefaults;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Database
builder.AddDatabase();

// Authentication & Authorization
builder.Services.AddAuthenticationServices(builder.Configuration);

// CORS
builder.Services.AddCorsPolicy(builder.Configuration);

// Rate Limiting
builder.Services.AddRateLimitingPolicies();

// Application Services
builder.Services.AddApplicationServices();

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
app.UseRateLimiter();

// Authentication & Authorization middleware
app.UseAuthentication();
app.UseAuthorization();

// Register endpoint groups
app.MapAuthEndpoints();
app.MapPortfolioEndpoints();
app.MapAccountGroupEndpoints();
app.MapAccountEndpoints();
app.MapSnapshotEndpoints();
app.MapImportEndpoints();

app.Run();
