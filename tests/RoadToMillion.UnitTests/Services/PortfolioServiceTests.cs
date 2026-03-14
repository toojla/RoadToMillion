using Microsoft.EntityFrameworkCore;
using RoadToMillion.Api.Data;
using RoadToMillion.Api.Models;
using RoadToMillion.Api.Services;
using Shouldly;

namespace RoadToMillion.UnitTests.Services;

/// <summary>
/// Unit tests for PortfolioService demonstrating how services are now easily testable.
/// </summary>
public class PortfolioServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly IPortfolioService _sut;

    public PortfolioServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .EnableSensitiveDataLogging()
            .Options;

        _db = new AppDbContext(options);
        _sut = new PortfolioService(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    [Fact]
    public async Task GetPortfolioSummaryAsync_WithNoData_ShouldReturnZeroValues()
    {
        // Act
        var result = await _sut.GetPortfolioSummaryAsync();

        // Assert
        result.ShouldNotBeNull();
        result.CurrentTotal.ShouldBe(0m);
        result.GoalAmount.ShouldBe(1_000_000m);
        result.RemainingAmount.ShouldBe(1_000_000m);
        result.ProgressPercentage.ShouldBe(0m);
        result.Groups.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetPortfolioSummaryAsync_WithData_ShouldCalculateCorrectly()
    {
        // Arrange
        var group = new AccountGroup { Name = "Savings" };
        var account = new Account { Name = "Main Account", AccountGroup = group };
        var snapshot = new BalanceSnapshot
        {
            Account = account,
            Amount = 250_000m,
            Date = DateOnly.FromDateTime(DateTime.Today),
            RecordedAt = DateTime.UtcNow
        };

        account.BalanceSnapshots.Add(snapshot);
        group.Accounts.Add(account);
        _db.AccountGroups.Add(group);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetPortfolioSummaryAsync();

        // Assert
        result.ShouldNotBeNull();
        result.CurrentTotal.ShouldBe(250_000m);
        result.GoalAmount.ShouldBe(1_000_000m);
        result.RemainingAmount.ShouldBe(750_000m);
        result.ProgressPercentage.ShouldBe(25m);
        result.Groups.Count().ShouldBe(1);
        result.Groups.First().Name.ShouldBe("Savings");
        result.Groups.First().CurrentTotal.ShouldBe(250_000m);
    }

    [Fact]
    public async Task GetPortfolioSummaryAsync_WithMultipleSnapshots_ShouldUseMostRecent()
    {
        // Arrange
        var group = new AccountGroup { Name = "Savings" };
        var account = new Account { Name = "Main Account", AccountGroup = group };
        
        // Add older snapshot
        var oldSnapshot = new BalanceSnapshot
        {
            Account = account,
            Amount = 100_000m,
            Date = DateOnly.FromDateTime(DateTime.Today.AddDays(-10)),
            RecordedAt = DateTime.UtcNow.AddDays(-10)
        };
        
        // Add newer snapshot
        var newSnapshot = new BalanceSnapshot
        {
            Account = account,
            Amount = 150_000m,
            Date = DateOnly.FromDateTime(DateTime.Today),
            RecordedAt = DateTime.UtcNow
        };

        account.BalanceSnapshots.Add(oldSnapshot);
        account.BalanceSnapshots.Add(newSnapshot);
        group.Accounts.Add(account);
        _db.AccountGroups.Add(group);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetPortfolioSummaryAsync();

        // Assert
        result.CurrentTotal.ShouldBe(150_000m); // Should use the newer snapshot
    }

    [Fact]
    public async Task GetPortfolioSummaryAsync_WithMultipleGroupsAndAccounts_ShouldSumCorrectly()
    {
        // Arrange
        var savingsGroup = new AccountGroup { Name = "Savings" };
        var investmentGroup = new AccountGroup { Name = "Investments" };

        var savingsAccount = new Account { Name = "Bank Account", AccountGroup = savingsGroup };
        var stocksAccount = new Account { Name = "Stocks", AccountGroup = investmentGroup };
        var cryptoAccount = new Account { Name = "Crypto", AccountGroup = investmentGroup };

        savingsAccount.BalanceSnapshots.Add(new BalanceSnapshot
        {
            Account = savingsAccount,
            Amount = 50_000m,
            Date = DateOnly.FromDateTime(DateTime.Today),
            RecordedAt = DateTime.UtcNow
        });

        stocksAccount.BalanceSnapshots.Add(new BalanceSnapshot
        {
            Account = stocksAccount,
            Amount = 200_000m,
            Date = DateOnly.FromDateTime(DateTime.Today),
            RecordedAt = DateTime.UtcNow
        });

        cryptoAccount.BalanceSnapshots.Add(new BalanceSnapshot
        {
            Account = cryptoAccount,
            Amount = 50_000m,
            Date = DateOnly.FromDateTime(DateTime.Today),
            RecordedAt = DateTime.UtcNow
        });

        savingsGroup.Accounts.Add(savingsAccount);
        investmentGroup.Accounts.Add(stocksAccount);
        investmentGroup.Accounts.Add(cryptoAccount);

        _db.AccountGroups.Add(savingsGroup);
        _db.AccountGroups.Add(investmentGroup);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetPortfolioSummaryAsync();

        // Assert
        result.CurrentTotal.ShouldBe(300_000m);
        result.Groups.Count().ShouldBe(2);
        
        var savings = result.Groups.First(g => g.Name == "Savings");
        savings.CurrentTotal.ShouldBe(50_000m);
        
        var investments = result.Groups.First(g => g.Name == "Investments");
        investments.CurrentTotal.ShouldBe(250_000m);
    }

    [Fact]
    public async Task GetPortfolioSummaryAsync_WhenOverGoal_ShouldCapProgressAt100()
    {
        // Arrange
        var group = new AccountGroup { Name = "Savings" };
        var account = new Account { Name = "Main Account", AccountGroup = group };
        var snapshot = new BalanceSnapshot
        {
            Account = account,
            Amount = 1_500_000m, // More than the goal
            Date = DateOnly.FromDateTime(DateTime.Today),
            RecordedAt = DateTime.UtcNow
        };

        account.BalanceSnapshots.Add(snapshot);
        group.Accounts.Add(account);
        _db.AccountGroups.Add(group);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetPortfolioSummaryAsync();

        // Assert
        result.CurrentTotal.ShouldBe(1_500_000m);
        result.RemainingAmount.ShouldBe(0m);
        result.ProgressPercentage.ShouldBe(100m); // Should be capped at 100%
    }
}
