namespace RoadToMillion.Api.Endpoints;

public static class AccountGroupEndpoints
{
    public static void MapAccountGroupEndpoints(this WebApplication app)
    {
        var groups = app.MapGroup("/api/account-groups").RequireAuthorization();

        groups.MapGet("", async (IAccountGroupService accountGroupService) =>
        {
            var groupsList = await accountGroupService.GetAllAccountGroupsAsync();
            return Results.Ok(groupsList);
        });

        groups.MapPost("", async (IAccountGroupService accountGroupService, CreateAccountGroupRequest req) =>
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

        groups.MapDelete("/{id:int}", async (int id, IAccountGroupService accountGroupService) =>
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