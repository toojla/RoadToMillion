using Microsoft.EntityFrameworkCore;
using RoadToMillion.Api.Data;
using RoadToMillion.Api.Models;
using RoadToMillion.Api.Services;
using Shouldly;

namespace RoadToMillion.UnitTests.Services;

/// <summary>
/// Unit tests for SnapshotService demonstrating balance snapshot management.
/// </summary>
public class SnapshotServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly ISnapshotService _sut;

    public SnapshotServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .EnableSensitiveDataLogging()
            .Options;

        _db = new AppDbContext(options);
        _sut = new SnapshotService(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    #region GetSnapshotsByAccountAsync Tests

    [Fact]
    public async Task GetSnapshotsByAccountAsync_WithNonExistentAccount_ShouldReturnNotFound()
    {
        // Act
        var result = await _sut.GetSnapshotsByAccountAsync(999);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Type.ShouldBe(ResultType.NotFound);
    }

    [Fact]
    public async Task GetSnapshotsByAccountAsync_WithNoSnapshots_ShouldReturnEmptyList()
    {
        // Arrange
        var group = new AccountGroup { Name = "Savings" };
        var account = new Account { Name = "Main Account", AccountGroup = group };
        group.Accounts.Add(account);
        _db.AccountGroups.Add(group);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetSnapshotsByAccountAsync(account.Id);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Data.ShouldNotBeNull();
        result.Data.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetSnapshotsByAccountAsync_WithSnapshots_ShouldReturnAll()
    {
        // Arrange
        var group = new AccountGroup { Name = "Savings" };
        var account = new Account { Name = "Main Account", AccountGroup = group };
        
        var snapshot1 = new BalanceSnapshot
        {
            Account = account,
            Amount = 1000m,
            Date = DateOnly.FromDateTime(DateTime.Today.AddDays(-10)),
            RecordedAt = DateTime.UtcNow.AddDays(-10)
        };
        
        var snapshot2 = new BalanceSnapshot
        {
            Account = account,
            Amount = 1500m,
            Date = DateOnly.FromDateTime(DateTime.Today),
            RecordedAt = DateTime.UtcNow
        };

        account.BalanceSnapshots.Add(snapshot1);
        account.BalanceSnapshots.Add(snapshot2);
        group.Accounts.Add(account);
        _db.AccountGroups.Add(group);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetSnapshotsByAccountAsync(account.Id);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Data.ShouldNotBeNull();
        result.Data.Count().ShouldBe(2);
    }

    [Fact]
    public async Task GetSnapshotsByAccountAsync_ShouldOrderByDateDescending()
    {
        // Arrange
        var group = new AccountGroup { Name = "Savings" };
        var account = new Account { Name = "Main Account", AccountGroup = group };
        
        var oldSnapshot = new BalanceSnapshot
        {
            Account = account,
            Amount = 1000m,
            Date = DateOnly.FromDateTime(DateTime.Today.AddDays(-20)),
            RecordedAt = DateTime.UtcNow.AddDays(-20)
        };
        
        var midSnapshot = new BalanceSnapshot
        {
            Account = account,
            Amount = 1500m,
            Date = DateOnly.FromDateTime(DateTime.Today.AddDays(-10)),
            RecordedAt = DateTime.UtcNow.AddDays(-10)
        };
        
        var newSnapshot = new BalanceSnapshot
        {
            Account = account,
            Amount = 2000m,
            Date = DateOnly.FromDateTime(DateTime.Today),
            RecordedAt = DateTime.UtcNow
        };

        account.BalanceSnapshots.Add(oldSnapshot);
        account.BalanceSnapshots.Add(midSnapshot);
        account.BalanceSnapshots.Add(newSnapshot);
        group.Accounts.Add(account);
        _db.AccountGroups.Add(group);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetSnapshotsByAccountAsync(account.Id);

        // Assert
        var snapshots = result.Data!.ToList();
        snapshots[0].Amount.ShouldBe(2000m); // Most recent first
        snapshots[1].Amount.ShouldBe(1500m);
        snapshots[2].Amount.ShouldBe(1000m);
    }

    [Fact]
    public async Task GetSnapshotsByAccountAsync_ShouldMarkMostRecentSnapshot()
    {
        // Arrange
        var group = new AccountGroup { Name = "Savings" };
        var account = new Account { Name = "Main Account", AccountGroup = group };
        
        var oldSnapshot = new BalanceSnapshot
        {
            Account = account,
            Amount = 1000m,
            Date = DateOnly.FromDateTime(DateTime.Today.AddDays(-10)),
            RecordedAt = DateTime.UtcNow.AddDays(-10)
        };
        
        var newSnapshot = new BalanceSnapshot
        {
            Account = account,
            Amount = 1500m,
            Date = DateOnly.FromDateTime(DateTime.Today),
            RecordedAt = DateTime.UtcNow
        };

        account.BalanceSnapshots.Add(oldSnapshot);
        account.BalanceSnapshots.Add(newSnapshot);
        group.Accounts.Add(account);
        _db.AccountGroups.Add(group);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetSnapshotsByAccountAsync(account.Id);

        // Assert
        var snapshots = result.Data!.ToList();
        snapshots[0].IsMostRecent.ShouldBeTrue();  // Newest
        snapshots[1].IsMostRecent.ShouldBeFalse(); // Older
    }

    [Fact]
    public async Task GetSnapshotsByAccountAsync_WithSameDateDifferentRecordedAt_ShouldUseRecordedAt()
    {
        // Arrange
        var group = new AccountGroup { Name = "Savings" };
        var account = new Account { Name = "Main Account", AccountGroup = group };
        
        var snapshot1 = new BalanceSnapshot
        {
            Account = account,
            Amount = 1000m,
            Date = DateOnly.FromDateTime(DateTime.Today),
            RecordedAt = DateTime.UtcNow.AddHours(-1)
        };
        
        var snapshot2 = new BalanceSnapshot
        {
            Account = account,
            Amount = 1500m,
            Date = DateOnly.FromDateTime(DateTime.Today),
            RecordedAt = DateTime.UtcNow
        };

        account.BalanceSnapshots.Add(snapshot1);
        account.BalanceSnapshots.Add(snapshot2);
        group.Accounts.Add(account);
        _db.AccountGroups.Add(group);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetSnapshotsByAccountAsync(account.Id);

        // Assert
        var snapshots = result.Data!.ToList();
        snapshots[0].Amount.ShouldBe(1500m); // Most recently recorded
        snapshots[0].IsMostRecent.ShouldBeTrue();
    }

    #endregion

    #region CreateSnapshotAsync Tests

    [Fact]
    public async Task CreateSnapshotAsync_WithNonExistentAccount_ShouldReturnNotFound()
    {
        // Act
        var result = await _sut.CreateSnapshotAsync(999, 1000m, DateOnly.FromDateTime(DateTime.Today));

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Type.ShouldBe(ResultType.NotFound);
    }

    [Fact]
    public async Task CreateSnapshotAsync_WithValidData_ShouldCreateSnapshot()
    {
        // Arrange
        var group = new AccountGroup { Name = "Savings" };
        var account = new Account { Name = "Main Account", AccountGroup = group };
        group.Accounts.Add(account);
        _db.AccountGroups.Add(group);
        await _db.SaveChangesAsync();

        var date = DateOnly.FromDateTime(DateTime.Today);

        // Act
        var result = await _sut.CreateSnapshotAsync(account.Id, 1500.50m, date);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Type.ShouldBe(ResultType.Created);
        result.Data.ShouldNotBeNull();
        result.Data.Amount.ShouldBe(1500.50m);
        result.Data.Date.ShouldBe(date);
        result.Data.IsMostRecent.ShouldBeTrue();
        result.Location.ShouldBe($"/api/accounts/{account.Id}/snapshots/{result.Data.Id}");

        // Verify in database
        var saved = await _db.BalanceSnapshots.FirstOrDefaultAsync();
        saved.ShouldNotBeNull();
        saved.Amount.ShouldBe(1500.50m);
        saved.AccountId.ShouldBe(account.Id);
    }

    [Fact]
    public async Task CreateSnapshotAsync_WithZeroAmount_ShouldReturnBadRequest()
    {
        // Arrange
        var group = new AccountGroup { Name = "Savings" };
        var account = new Account { Name = "Main Account", AccountGroup = group };
        group.Accounts.Add(account);
        _db.AccountGroups.Add(group);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.CreateSnapshotAsync(account.Id, 0m, DateOnly.FromDateTime(DateTime.Today));

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Type.ShouldBe(ResultType.BadRequest);
        result.ErrorMessage.ShouldBe("Amount must be greater than zero.");
    }

    [Fact]
    public async Task CreateSnapshotAsync_WithNegativeAmount_ShouldReturnBadRequest()
    {
        // Arrange
        var group = new AccountGroup { Name = "Savings" };
        var account = new Account { Name = "Main Account", AccountGroup = group };
        group.Accounts.Add(account);
        _db.AccountGroups.Add(group);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.CreateSnapshotAsync(account.Id, -100m, DateOnly.FromDateTime(DateTime.Today));

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Type.ShouldBe(ResultType.BadRequest);
    }

    [Fact]
    public async Task CreateSnapshotAsync_AsNewerSnapshot_ShouldBeMarkedAsMostRecent()
    {
        // Arrange
        var group = new AccountGroup { Name = "Savings" };
        var account = new Account { Name = "Main Account", AccountGroup = group };
        
        var oldSnapshot = new BalanceSnapshot
        {
            Account = account,
            Amount = 1000m,
            Date = DateOnly.FromDateTime(DateTime.Today.AddDays(-10)),
            RecordedAt = DateTime.UtcNow.AddDays(-10)
        };
        account.BalanceSnapshots.Add(oldSnapshot);
        group.Accounts.Add(account);
        _db.AccountGroups.Add(group);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.CreateSnapshotAsync(account.Id, 2000m, DateOnly.FromDateTime(DateTime.Today));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Data!.IsMostRecent.ShouldBeTrue();
    }

    [Fact]
    public async Task CreateSnapshotAsync_AsOlderSnapshot_ShouldNotBeMarkedAsMostRecent()
    {
        // Arrange
        var group = new AccountGroup { Name = "Savings" };
        var account = new Account { Name = "Main Account", AccountGroup = group };
        
        var newSnapshot = new BalanceSnapshot
        {
            Account = account,
            Amount = 2000m,
            Date = DateOnly.FromDateTime(DateTime.Today),
            RecordedAt = DateTime.UtcNow
        };
        account.BalanceSnapshots.Add(newSnapshot);
        group.Accounts.Add(account);
        _db.AccountGroups.Add(group);
        await _db.SaveChangesAsync();

        // Act - Create an older snapshot
        var result = await _sut.CreateSnapshotAsync(account.Id, 1000m, DateOnly.FromDateTime(DateTime.Today.AddDays(-10)));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Data!.IsMostRecent.ShouldBeFalse();
    }

    [Fact]
    public async Task CreateSnapshotAsync_ShouldSetRecordedAtToUtcNow()
    {
        // Arrange
        var group = new AccountGroup { Name = "Savings" };
        var account = new Account { Name = "Main Account", AccountGroup = group };
        group.Accounts.Add(account);
        _db.AccountGroups.Add(group);
        await _db.SaveChangesAsync();

        var beforeCreate = DateTime.UtcNow;

        // Act
        var result = await _sut.CreateSnapshotAsync(account.Id, 1000m, DateOnly.FromDateTime(DateTime.Today));

        var afterCreate = DateTime.UtcNow;

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Data!.RecordedAt.ShouldBeInRange(beforeCreate, afterCreate);
        result.Data.RecordedAt.Kind.ShouldBe(DateTimeKind.Utc);
    }

    [Fact]
    public async Task CreateSnapshotAsync_WithFutureDate_ShouldSucceed()
    {
        // Arrange
        var group = new AccountGroup { Name = "Savings" };
        var account = new Account { Name = "Main Account", AccountGroup = group };
        group.Accounts.Add(account);
        _db.AccountGroups.Add(group);
        await _db.SaveChangesAsync();

        var futureDate = DateOnly.FromDateTime(DateTime.Today.AddDays(30));

        // Act
        var result = await _sut.CreateSnapshotAsync(account.Id, 1000m, futureDate);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Data!.Date.ShouldBe(futureDate);
    }

    [Fact]
    public async Task CreateSnapshotAsync_WithPastDate_ShouldSucceed()
    {
        // Arrange
        var group = new AccountGroup { Name = "Savings" };
        var account = new Account { Name = "Main Account", AccountGroup = group };
        group.Accounts.Add(account);
        _db.AccountGroups.Add(group);
        await _db.SaveChangesAsync();

        var pastDate = DateOnly.FromDateTime(DateTime.Today.AddYears(-1));

        // Act
        var result = await _sut.CreateSnapshotAsync(account.Id, 1000m, pastDate);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Data!.Date.ShouldBe(pastDate);
    }

    #endregion
}
