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
            Amount = 1_500_000m,
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
        result.ProgressPercentage.ShouldBe(100m);
    }

    [Fact]
    public async Task GetPortfolioSummaryAsync_WithPensionAccounts_ShouldExcludeFromGoalTotal()
    {
        // Arrange
        var group = new AccountGroup { Name = "Mixed" };
        var regular = new Account { Name = "ISK", AccountGroup = group, Type = AccountType.Regular };
        var pension = new Account { Name = "ITP2", AccountGroup = group, Type = AccountType.ServicePension };

        regular.BalanceSnapshots.Add(new BalanceSnapshot
        {
            Account = regular,
            Amount = 200_000m,
            Date = DateOnly.FromDateTime(DateTime.Today),
            RecordedAt = DateTime.UtcNow
        });

        pension.BalanceSnapshots.Add(new BalanceSnapshot
        {
            Account = pension,
            Amount = 300_000m,
            Date = DateOnly.FromDateTime(DateTime.Today),
            RecordedAt = DateTime.UtcNow
        });

        group.Accounts.Add(regular);
        group.Accounts.Add(pension);
        _db.AccountGroups.Add(group);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetPortfolioSummaryAsync();

        // Assert
        result.CurrentTotal.ShouldBe(200_000m);          // pension excluded
        result.PensionTotal.ShouldBe(300_000m);           // pension tracked separately
        result.RemainingAmount.ShouldBe(800_000m);
        result.Groups.First().CurrentTotal.ShouldBe(200_000m);
    }

    [Fact]
    public async Task GetPortfolioSummaryAsync_WithSingleSnapshotDate_ShouldReturnAsOfDateButNoChange()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.Today);
        var group = new AccountGroup { Name = "Savings" };
        var account = new Account { Name = "Main", AccountGroup = group };
        account.BalanceSnapshots.Add(new BalanceSnapshot
        {
            Account = account,
            Amount = 100_000m,
            Date = today,
            RecordedAt = DateTime.UtcNow
        });
        group.Accounts.Add(account);
        _db.AccountGroups.Add(group);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetPortfolioSummaryAsync();

        // Assert
        result.CurrentAsOfDate.ShouldBe(today);
        result.PreviousAsOfDate.ShouldBeNull();
        result.PreviousTotal.ShouldBeNull();
        result.ChangeAmount.ShouldBeNull();
        result.ChangePercentage.ShouldBeNull();
    }

    [Fact]
    public async Task GetPortfolioSummaryAsync_WithTwoSnapshotDates_ShouldComputeChangePercentage()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.Today);
        var lastWeek = today.AddDays(-7);
        var group = new AccountGroup { Name = "Savings" };
        var account = new Account { Name = "Main", AccountGroup = group };
        account.BalanceSnapshots.Add(new BalanceSnapshot
        {
            Account = account,
            Amount = 100_000m,
            Date = lastWeek,
            RecordedAt = DateTime.UtcNow.AddDays(-7)
        });
        account.BalanceSnapshots.Add(new BalanceSnapshot
        {
            Account = account,
            Amount = 110_000m,
            Date = today,
            RecordedAt = DateTime.UtcNow
        });
        group.Accounts.Add(account);
        _db.AccountGroups.Add(group);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetPortfolioSummaryAsync();

        // Assert
        result.CurrentTotal.ShouldBe(110_000m);
        result.CurrentAsOfDate.ShouldBe(today);
        result.PreviousAsOfDate.ShouldBe(lastWeek);
        result.PreviousTotal.ShouldBe(100_000m);
        result.ChangeAmount.ShouldBe(10_000m);
        result.ChangePercentage.ShouldBe(10m);
    }

    [Fact]
    public async Task GetPortfolioSummaryAsync_WithPreviousTotalZero_ShouldReturnAmountButNullPercentage()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.Today);
        var yesterday = today.AddDays(-1);
        var group = new AccountGroup { Name = "Savings" };
        var account = new Account { Name = "Main", AccountGroup = group };
        account.BalanceSnapshots.Add(new BalanceSnapshot
        {
            Account = account,
            Amount = 0m,
            Date = yesterday,
            RecordedAt = DateTime.UtcNow.AddDays(-1)
        });
        account.BalanceSnapshots.Add(new BalanceSnapshot
        {
            Account = account,
            Amount = 50_000m,
            Date = today,
            RecordedAt = DateTime.UtcNow
        });
        group.Accounts.Add(account);
        _db.AccountGroups.Add(group);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetPortfolioSummaryAsync();

        // Assert
        result.PreviousTotal.ShouldBe(0m);
        result.ChangeAmount.ShouldBe(50_000m);
        result.ChangePercentage.ShouldBeNull();
    }

    [Fact]
    public async Task GetPortfolioSummaryAsync_AccountMissingOnPreviousDate_ShouldUseEarlierSnapshot()
    {
        // Arrange — Account A has snapshots on both dates, Account B only on today.
        var today = DateOnly.FromDateTime(DateTime.Today);
        var lastWeek = today.AddDays(-7);
        var group = new AccountGroup { Name = "Mixed" };

        var a = new Account { Name = "A", AccountGroup = group };
        a.BalanceSnapshots.Add(new BalanceSnapshot { Account = a, Amount = 100_000m, Date = lastWeek, RecordedAt = DateTime.UtcNow.AddDays(-7) });
        a.BalanceSnapshots.Add(new BalanceSnapshot { Account = a, Amount = 120_000m, Date = today,    RecordedAt = DateTime.UtcNow });

        var b = new Account { Name = "B", AccountGroup = group };
        b.BalanceSnapshots.Add(new BalanceSnapshot { Account = b, Amount = 30_000m, Date = today, RecordedAt = DateTime.UtcNow });

        group.Accounts.Add(a);
        group.Accounts.Add(b);
        _db.AccountGroups.Add(group);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetPortfolioSummaryAsync();

        // Assert
        result.CurrentTotal.ShouldBe(150_000m);           // 120k + 30k
        result.PreviousTotal.ShouldBe(100_000m);          // 100k + 0 (B did not exist yet)
        result.ChangeAmount.ShouldBe(50_000m);
        result.ChangePercentage.ShouldBe(50m);
    }

    [Fact]
    public async Task GetPortfolioSummaryAsync_PensionAndRegularHaveDifferentCadences_ShouldReportIndependently()
    {
        // Arrange — Regular has two dates, Pension has only one.
        var today = DateOnly.FromDateTime(DateTime.Today);
        var lastWeek = today.AddDays(-7);
        var group = new AccountGroup { Name = "Mixed" };

        var regular = new Account { Name = "ISK", AccountGroup = group, Type = AccountType.Regular };
        regular.BalanceSnapshots.Add(new BalanceSnapshot { Account = regular, Amount = 100_000m, Date = lastWeek, RecordedAt = DateTime.UtcNow.AddDays(-7) });
        regular.BalanceSnapshots.Add(new BalanceSnapshot { Account = regular, Amount = 105_000m, Date = today,    RecordedAt = DateTime.UtcNow });

        var pension = new Account { Name = "ITP2", AccountGroup = group, Type = AccountType.ServicePension };
        pension.BalanceSnapshots.Add(new BalanceSnapshot { Account = pension, Amount = 200_000m, Date = today, RecordedAt = DateTime.UtcNow });

        group.Accounts.Add(regular);
        group.Accounts.Add(pension);
        _db.AccountGroups.Add(group);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetPortfolioSummaryAsync();

        // Assert — Regular delta populated
        result.CurrentAsOfDate.ShouldBe(today);
        result.PreviousAsOfDate.ShouldBe(lastWeek);
        result.ChangePercentage.ShouldBe(5m);

        // Assert — Pension delta absent
        result.PensionAsOfDate.ShouldBe(today);
        result.PreviousPensionAsOfDate.ShouldBeNull();
        result.PreviousPensionTotal.ShouldBeNull();
        result.PensionChangeAmount.ShouldBeNull();
        result.PensionChangePercentage.ShouldBeNull();
    }

    [Fact]
    public async Task GetPortfolioSummaryAsync_NegativeChange_ShouldReturnNegativePercentage()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.Today);
        var lastWeek = today.AddDays(-7);
        var group = new AccountGroup { Name = "Savings" };
        var account = new Account { Name = "Main", AccountGroup = group };
        account.BalanceSnapshots.Add(new BalanceSnapshot { Account = account, Amount = 200_000m, Date = lastWeek, RecordedAt = DateTime.UtcNow.AddDays(-7) });
        account.BalanceSnapshots.Add(new BalanceSnapshot { Account = account, Amount = 180_000m, Date = today,    RecordedAt = DateTime.UtcNow });
        group.Accounts.Add(account);
        _db.AccountGroups.Add(group);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetPortfolioSummaryAsync();

        // Assert
        result.ChangeAmount.ShouldBe(-20_000m);
        result.ChangePercentage.ShouldBe(-10m);
    }
}
