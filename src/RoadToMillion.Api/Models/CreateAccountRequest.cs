namespace RoadToMillion.Api.Models;

public record CreateAccountRequest(string Name, string? Description, AccountType Type = AccountType.Regular);