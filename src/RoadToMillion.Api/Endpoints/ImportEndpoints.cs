namespace RoadToMillion.Api.Endpoints;

public static class ImportEndpoints
{
    public static void MapImportEndpoints(this WebApplication app)
    {
        var import = app.MapGroup("/api/import").RequireAuthorization();

        import.MapPost("/preview", async (IFormFile file, IImportService importService) =>
        {
            var result = await importService.ParsePreviewAsync(file);
            return result.Type switch
            {
                ResultType.Success => Results.Ok(result.Data),
                ResultType.BadRequest => Results.BadRequest(new { errors = new { file = new[] { result.ErrorMessage } }, status = 400 }),
                _ => Results.Problem("An unexpected error occurred", statusCode: 500)
            };
        }).DisableAntiforgery();

        import.MapPost("/confirm", async (ImportPreview preview, IImportService importService) =>
        {
            var result = await importService.ExecuteImportAsync(preview);
            return result.Type switch
            {
                ResultType.Success => Results.Ok(result.Data),
                ResultType.BadRequest => Results.BadRequest(new { errors = new { body = new[] { result.ErrorMessage } }, status = 400 }),
                ResultType.Conflict => Results.Conflict(new { error = result.ErrorMessage }),
                ResultType.Error => Results.Problem(detail: result.ErrorMessage, statusCode: 500),
                _ => Results.Problem("An unexpected error occurred", statusCode: 500)
            };
        });
    }
}