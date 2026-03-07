using Microsoft.EntityFrameworkCore;
using RoadToMillion.Api.Data;
using RoadToMillion.Api.Models;
using RoadToMillion.Api.Services;

namespace RoadToMillion.Api.Endpoints;

public static class ImportEndpoints
{
    public static void MapImportEndpoints(this WebApplication app)
    {
        app.MapPost("/api/import/preview", async (IFormFile file, CsvImportService importService) =>
        {
            try
            {
                var preview = await importService.ParsePreviewAsync(file);
                return Results.Ok(preview);
            }
            catch (ImportValidationException ex)
            {
                return Results.BadRequest(new { errors = new { file = new[] { ex.Message } }, status = 400 });
            }
        }).DisableAntiforgery();

        app.MapPost("/api/import/confirm", async (ImportPreview preview, CsvImportService importService, AppDbContext db) =>
        {
            if (preview.Groups.All(g => g.Accounts.All(a => a.WillBeSkipped)))
                return Results.BadRequest(new { errors = new { body = new[] { "No valid records to import." } }, status = 400 });

            // Server-side conflict check: any group not marked alreadyExists that now exists in DB?
            var newGroupNames = preview.Groups
                .Where(g => !g.AlreadyExists)
                .Select(g => g.Name.ToLower())
                .ToList();

            if (newGroupNames.Count > 0)
            {
                var nowExist = await db.AccountGroups
                    .Where(g => newGroupNames.Contains(g.Name.ToLower()))
                    .AnyAsync();

                if (nowExist)
                    return Results.Conflict(new
                    {
                        error = "Data changed since preview was generated. Please re-upload and preview again."
                    });
            }

            try
            {
                var result = await importService.ExecuteImportAsync(preview);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: 500);
            }
        });
    }
}
