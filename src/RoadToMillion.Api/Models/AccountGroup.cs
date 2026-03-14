namespace RoadToMillion.Api.Models;

public class AccountGroup
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ICollection<Account> Accounts { get; set; } = new List<Account>();
}
