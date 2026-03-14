namespace RoadToMillion.Api.Endpoints;

public static class PortfolioEndpoints
{
    public static void MapPortfolioEndpoints(this WebApplication app)
    {
        app.MapGet("/api/portfolio/summary", async (IPortfolioService portfolioService) =>
        {
            var summary = await portfolioService.GetPortfolioSummaryAsync();
            return Results.Ok(summary);
        });
    }
}