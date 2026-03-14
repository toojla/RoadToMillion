namespace RoadToMillion.Api.Services;

public interface IPortfolioService
{
    Task<PortfolioSummary> GetPortfolioSummaryAsync();
}