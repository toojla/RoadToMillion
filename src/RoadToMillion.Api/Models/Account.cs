namespace RoadToMillion.Api.Models;

public class Account
{
    public int Id { get; set; }
    public int AccountGroupId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public AccountGroup AccountGroup { get; set; } = null!;
    public ICollection<BalanceSnapshot> BalanceSnapshots { get; set; } = new List<BalanceSnapshot>();
}
