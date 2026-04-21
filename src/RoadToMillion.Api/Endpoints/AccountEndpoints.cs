namespace RoadToMillion.Api.Endpoints;

public static class AccountEndpoints
{
    public static void MapAccountEndpoints(this WebApplication app)
    {
        var accounts = app.MapGroup("/api").RequireAuthorization();

        accounts.MapGet("/account-groups/{groupId:int}/accounts", async (int groupId, IAccountService accountService) =>
        {
            var result = await accountService.GetAccountsByGroupAsync(groupId);
            return result.Type switch
            {
                ResultType.Success => Results.Ok(result.Data),
                ResultType.NotFound => Results.NotFound(),
                _ => Results.Problem("An unexpected error occurred", statusCode: 500)
            };
        });

        accounts.MapPost("/account-groups/{groupId:int}/accounts", async (int groupId, CreateAccountRequest req, IAccountService accountService) =>
        {
            var result = await accountService.CreateAccountAsync(groupId, req.Name, req.Description, req.Type);
            return result.Type switch
            {
                ResultType.Created => Results.Created(result.Location!, result.Data),
                ResultType.NotFound => Results.NotFound(),
                ResultType.BadRequest => Results.BadRequest(new { errors = new { name = new[] { result.ErrorMessage } } }),
                ResultType.Conflict => Results.Conflict(new { errors = new { name = new[] { result.ErrorMessage } } }),
                _ => Results.Problem("An unexpected error occurred", statusCode: 500)
            };
        });

        accounts.MapDelete("/accounts/{id:int}", async (int id, IAccountService accountService) =>
        {
            var result = await accountService.DeleteAccountAsync(id);
            return result.Type switch
            {
                ResultType.NoContent => Results.NoContent(),
                ResultType.NotFound => Results.NotFound(),
                _ => Results.Problem("An unexpected error occurred", statusCode: 500)
            };
        });

        accounts.MapGet("/accounts/{id:int}", async (int id, IAccountService accountService) =>
        {
            var result = await accountService.GetAccountByIdAsync(id);
            return result.Type switch
            {
                ResultType.Success => Results.Ok(result.Data),
                ResultType.NotFound => Results.NotFound(),
                _ => Results.Problem("An unexpected error occurred", statusCode: 500)
            };
        });
    }
}