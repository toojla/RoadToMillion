using RoadToMillion.Api.Models;
namespace RoadToMillion.Api.Endpoints;

public static class SnapshotEndpoints
{
    public static void MapSnapshotEndpoints(this WebApplication app)
    {
        app.MapGet("/api/accounts/{accountId:int}/snapshots", async (int accountId, ISnapshotService snapshotService) =>
        {
            var result = await snapshotService.GetSnapshotsByAccountAsync(accountId);
            return result.Type switch
            {
                ResultType.Success => Results.Ok(result.Data),
                ResultType.NotFound => Results.NotFound(),
                _ => Results.Problem("An unexpected error occurred", statusCode: 500)
            };
        });

        app.MapPost("/api/accounts/{accountId:int}/snapshots", async (int accountId, CreateSnapshotRequest req, ISnapshotService snapshotService) =>
        {
            var result = await snapshotService.CreateSnapshotAsync(accountId, req.Amount, req.Date);
            return result.Type switch
            {
                ResultType.Created => Results.Created(result.Location!, result.Data),
                ResultType.NotFound => Results.NotFound(),
                ResultType.BadRequest => Results.BadRequest(new { errors = new { amount = new[] { result.ErrorMessage } } }),
                _ => Results.Problem("An unexpected error occurred", statusCode: 500)
            };
        });
    }
}