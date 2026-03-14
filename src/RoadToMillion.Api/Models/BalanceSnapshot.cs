namespace RoadToMillion.Api.Models;

public class BalanceSnapshot
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public decimal Amount { get; set; }
    public DateOnly Date { get; set; }
    public DateTime RecordedAt { get; set; }

    public Account Account { get; set; } = null!;
}
