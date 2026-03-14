using RoadToMillion.Api.Models;
namespace RoadToMillion.Api.Endpoints;

public static class AccountGroupEndpoints
{
    public static void MapAccountGroupEndpoints(this WebApplication app)
    {
        app.MapGet("/api/account-groups", async (IAccountGroupService accountGroupService) =>
        {
            var groups = await accountGroupService.GetAllAccountGroupsAsync();
            return Results.Ok(groups);
        });

        app.MapPost("/api/account-groups", async (IAccountGroupService accountGroupService, CreateAccountGroupRequest req) =>
        {
            var result = await accountGroupService.CreateAccountGroupAsync(req.Name);
            return result.Type switch
            {
                ResultType.Created => Results.Created(result.Location!, result.Data),
                ResultType.BadRequest => Results.BadRequest(new { errors = new { name = new[] { result.ErrorMessage } } }),
                ResultType.Conflict => Results.Conflict(new { errors = new { name = new[] { result.ErrorMessage } } }),
                _ => Results.Problem("An unexpected error occurred", statusCode: 500)
            };
        });

        app.MapDelete("/api/account-groups/{id:int}", async (int id, IAccountGroupService accountGroupService) =>
        {
            var result = await accountGroupService.DeleteAccountGroupAsync(id);
            return result.Type switch
            {
                ResultType.NoContent => Results.NoContent(),
                ResultType.NotFound => Results.NotFound(),
                _ => Results.Problem("An unexpected error occurred", statusCode: 500)
            };
        });
    }
}