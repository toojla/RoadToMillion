using Microsoft.EntityFrameworkCore;
using RoadToMillion.Api.Data;
using RoadToMillion.Api.Models;
using RoadToMillion.Api.Services;
using Shouldly;

namespace RoadToMillion.UnitTests.Services;

/// <summary>
/// Unit tests for AccountGroupService demonstrating CRUD operations for account groups.
/// </summary>
public class AccountGroupServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly IAccountGroupService _sut;

    public AccountGroupServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .EnableSensitiveDataLogging()
            .Options;

        _db = new AppDbContext(options);
        _sut = new AccountGroupService(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    #region GetAllAccountGroupsAsync Tests

    [Fact]
    public async Task GetAllAccountGroupsAsync_WithNoData_ShouldReturnEmptyList()
    {
        // Act
        var result = await _sut.GetAllAccountGroupsAsync();

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetAllAccountGroupsAsync_WithGroups_ShouldReturnAllGroups()
    {
        // Arrange
        var group1 = new AccountGroup { Name = "Savings" };
        var group2 = new AccountGroup { Name = "Investments" };
        _db.AccountGroups.AddRange(group1, group2);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetAllAccountGroupsAsync();

        // Assert
        result.Count().ShouldBe(2);
        result.ShouldContain(g => g.Name == "Savings");
        result.ShouldContain(g => g.Name == "Investments");
    }

    [Fact]
    public async Task GetAllAccountGroupsAsync_ShouldCalculateCurrentTotal()
    {
        // Arrange
        var group = new AccountGroup { Name = "Savings" };
        var account = new Account { Name = "Main Account", AccountGroup = group };
        var snapshot = new BalanceSnapshot
        {
            Account = account,
            Amount = 1500.50m,
            Date = DateOnly.FromDateTime(DateTime.Today),
            RecordedAt = DateTime.UtcNow
        };

        account.BalanceSnapshots.Add(snapshot);
        group.Accounts.Add(account);
        _db.AccountGroups.Add(group);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetAllAccountGroupsAsync();

        // Assert
        var savingsGroup = result.First();
        savingsGroup.CurrentTotal.ShouldBe(1500.50m);
    }

    [Fact]
    public async Task GetAllAccountGroupsAsync_WithMultipleAccounts_ShouldSumTotals()
    {
        // Arrange
        var group = new AccountGroup { Name = "Savings" };
        var account1 = new Account { Name = "Account 1", AccountGroup = group };
        var account2 = new Account { Name = "Account 2", AccountGroup = group };

        account1.BalanceSnapshots.Add(new BalanceSnapshot
        {
            Account = account1,
            Amount = 1000m,
            Date = DateOnly.FromDateTime(DateTime.Today),
            RecordedAt = DateTime.UtcNow
        });

        account2.BalanceSnapshots.Add(new BalanceSnapshot
        {
            Account = account2,
            Amount = 2000m,
            Date = DateOnly.FromDateTime(DateTime.Today),
            RecordedAt = DateTime.UtcNow
        });

        group.Accounts.Add(account1);
        group.Accounts.Add(account2);
        _db.AccountGroups.Add(group);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetAllAccountGroupsAsync();

        // Assert
        result.First().CurrentTotal.ShouldBe(3000m);
    }

    [Fact]
    public async Task GetAllAccountGroupsAsync_WithMultipleSnapshots_ShouldUseMostRecent()
    {
        // Arrange
        var group = new AccountGroup { Name = "Savings" };
        var account = new Account { Name = "Main Account", AccountGroup = group };

        // Add older snapshot
        account.BalanceSnapshots.Add(new BalanceSnapshot
        {
            Account = account,
            Amount = 1000m,
            Date = DateOnly.FromDateTime(DateTime.Today.AddDays(-10)),
            RecordedAt = DateTime.UtcNow.AddDays(-10)
        });

        // Add newer snapshot
        account.BalanceSnapshots.Add(new BalanceSnapshot
        {
            Account = account,
            Amount = 1500m,
            Date = DateOnly.FromDateTime(DateTime.Today),
            RecordedAt = DateTime.UtcNow
        });

        group.Accounts.Add(account);
        _db.AccountGroups.Add(group);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetAllAccountGroupsAsync();

        // Assert
        result.First().CurrentTotal.ShouldBe(1500m); // Should use the newer snapshot
    }

    [Fact]
    public async Task GetAllAccountGroupsAsync_WithNoSnapshots_ShouldReturnZero()
    {
        // Arrange
        var group = new AccountGroup { Name = "Savings" };
        var account = new Account { Name = "Empty Account", AccountGroup = group };
        group.Accounts.Add(account);
        _db.AccountGroups.Add(group);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetAllAccountGroupsAsync();

        // Assert
        result.First().CurrentTotal.ShouldBe(0m);
    }

    #endregion

    #region CreateAccountGroupAsync Tests

    [Fact]
    public async Task CreateAccountGroupAsync_WithValidName_ShouldCreateGroup()
    {
        // Act
        var result = await _sut.CreateAccountGroupAsync("Savings");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Type.ShouldBe(ResultType.Created);
        result.Data.ShouldNotBeNull();
        result.Data.Name.ShouldBe("Savings");
        result.Data.CurrentTotal.ShouldBe(0m);
        result.Location.ShouldBe($"/api/account-groups/{result.Data.Id}");

        // Verify it was saved to database
        var saved = await _db.AccountGroups.FirstOrDefaultAsync();
        saved.ShouldNotBeNull();
        saved.Name.ShouldBe("Savings");
    }

    [Fact]
    public async Task CreateAccountGroupAsync_WithWhitespace_ShouldTrimName()
    {
        // Act
        var result = await _sut.CreateAccountGroupAsync("  Savings  ");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Data!.Name.ShouldBe("Savings");
    }

    [Fact]
    public async Task CreateAccountGroupAsync_WithEmptyName_ShouldReturnBadRequest()
    {
        // Act
        var result = await _sut.CreateAccountGroupAsync("");

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Type.ShouldBe(ResultType.BadRequest);
        result.ErrorMessage.ShouldBe("Name is required.");
    }

    [Fact]
    public async Task CreateAccountGroupAsync_WithWhitespaceName_ShouldReturnBadRequest()
    {
        // Act
        var result = await _sut.CreateAccountGroupAsync("   ");

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Type.ShouldBe(ResultType.BadRequest);
        result.ErrorMessage.ShouldBe("Name is required.");
    }

    [Fact]
    public async Task CreateAccountGroupAsync_WithDuplicateName_ShouldReturnConflict()
    {
        // Arrange
        var existing = new AccountGroup { Name = "Savings" };
        _db.AccountGroups.Add(existing);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.CreateAccountGroupAsync("Savings");

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Type.ShouldBe(ResultType.Conflict);
        result.ErrorMessage.ShouldBe("A group with this name already exists.");
    }

    [Fact]
    public async Task CreateAccountGroupAsync_WithDuplicateNameDifferentCase_ShouldReturnConflict()
    {
        // Arrange
        var existing = new AccountGroup { Name = "Savings" };
        _db.AccountGroups.Add(existing);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.CreateAccountGroupAsync("SAVINGS");

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Type.ShouldBe(ResultType.Conflict);
    }

    #endregion

    #region DeleteAccountGroupAsync Tests

    [Fact]
    public async Task DeleteAccountGroupAsync_WithExistingGroup_ShouldDelete()
    {
        // Arrange
        var group = new AccountGroup { Name = "Savings" };
        _db.AccountGroups.Add(group);
        await _db.SaveChangesAsync();
        var groupId = group.Id;

        // Act
        var result = await _sut.DeleteAccountGroupAsync(groupId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Type.ShouldBe(ResultType.NoContent);

        // Verify it was deleted
        var deleted = await _db.AccountGroups.FindAsync(groupId);
        deleted.ShouldBeNull();
    }

    [Fact]
    public async Task DeleteAccountGroupAsync_WithNonExistentId_ShouldReturnNotFound()
    {
        // Act
        var result = await _sut.DeleteAccountGroupAsync(999);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Type.ShouldBe(ResultType.NotFound);
    }

    [Fact]
    public async Task DeleteAccountGroupAsync_WithAccounts_ShouldDeleteCascade()
    {
        // Arrange
        var group = new AccountGroup { Name = "Savings" };
        var account = new Account { Name = "Main Account", AccountGroup = group };
        group.Accounts.Add(account);
        _db.AccountGroups.Add(group);
        await _db.SaveChangesAsync();
        var groupId = group.Id;

        // Act
        var result = await _sut.DeleteAccountGroupAsync(groupId);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        // Verify group and accounts were deleted (cascade delete configured in DbContext)
        var deletedGroup = await _db.AccountGroups.FindAsync(groupId);
        deletedGroup.ShouldBeNull();
    }

    #endregion
}
