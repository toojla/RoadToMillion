using Microsoft.EntityFrameworkCore;
using RoadToMillion.Api.Configuration;
using RoadToMillion.Api.Data;
using RoadToMillion.Api.Endpoints;
using RoadToMillion.ServiceDefaults;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Database
builder.AddDatabase();

// CORS
builder.Services.AddCorsPolicy(builder.Configuration);

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

// Register endpoint groups
app.MapPortfolioEndpoints();
app.MapAccountGroupEndpoints();
app.MapAccountEndpoints();
app.MapSnapshotEndpoints();
app.MapImportEndpoints();

app.Run();