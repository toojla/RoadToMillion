using Microsoft.EntityFrameworkCore;
using RoadToMillion.Api.Data;
using RoadToMillion.Api.Models;
using RoadToMillion.Api.Services;
using Shouldly;

namespace RoadToMillion.UnitTests.Services;

/// <summary>
/// Unit tests for AccountService demonstrating account management operations.
/// </summary>
public class AccountServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly IAccountService _sut;

    public AccountServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .EnableSensitiveDataLogging()
            .Options;

        _db = new AppDbContext(options);
        _sut = new AccountService(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    #region GetAccountsByGroupAsync Tests

    [Fact]
    public async Task GetAccountsByGroupAsync_WithNonExistentGroup_ShouldReturnNotFound()
    {
        // Act
        var result = await _sut.GetAccountsByGroupAsync(999);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Type.ShouldBe(ResultType.NotFound);
    }

    [Fact]
    public async Task GetAccountsByGroupAsync_WithEmptyGroup_ShouldReturnEmptyList()
    {
        // Arrange
        var group = new AccountGroup { Name = "Savings" };
        _db.AccountGroups.Add(group);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetAccountsByGroupAsync(group.Id);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Data.ShouldNotBeNull();
        result.Data.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetAccountsByGroupAsync_WithAccounts_ShouldReturnAll()
    {
        // Arrange
        var group = new AccountGroup { Name = "Savings" };
        var account1 = new Account { Name = "Account 1", AccountGroup = group };
        var account2 = new Account { Name = "Account 2", Description = "Test", AccountGroup = group };
        group.Accounts.Add(account1);
        group.Accounts.Add(account2);
        _db.AccountGroups.Add(group);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetAccountsByGroupAsync(group.Id);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Data.ShouldNotBeNull();
        result.Data.Count().ShouldBe(2);
        result.Data.ShouldContain(a => a.Name == "Account 1");
        result.Data.ShouldContain(a => a.Name == "Account 2" && a.Description == "Test");
    }

    [Fact]
    public async Task GetAccountsByGroupAsync_WithSnapshots_ShouldCalculateBalance()
    {
        // Arrange
        var group = new AccountGroup { Name = "Savings" };
        var account = new Account { Name = "Main Account", AccountGroup = group };
        var snapshot = new BalanceSnapshot
        {
            Account = account,
            Amount = 1500.75m,
            Date = DateOnly.FromDateTime(DateTime.Today),
            RecordedAt = DateTime.UtcNow
        };
        account.BalanceSnapshots.Add(snapshot);
        group.Accounts.Add(account);
        _db.AccountGroups.Add(group);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetAccountsByGroupAsync(group.Id);

        // Assert
        var accountResponse = result.Data!.First();
        accountResponse.CurrentBalance.ShouldBe(1500.75m);
        accountResponse.HasSnapshots.ShouldBeTrue();
    }

    [Fact]
    public async Task GetAccountsByGroupAsync_WithMultipleSnapshots_ShouldUseMostRecent()
    {
        // Arrange
        var group = new AccountGroup { Name = "Savings" };
        var account = new Account { Name = "Main Account", AccountGroup = group };
        
        account.BalanceSnapshots.Add(new BalanceSnapshot
        {
            Account = account,
            Amount = 1000m,
            Date = DateOnly.FromDateTime(DateTime.Today.AddDays(-10)),
            RecordedAt = DateTime.UtcNow.AddDays(-10)
        });
        
        account.BalanceSnapshots.Add(new BalanceSnapshot
        {
            Account = account,
            Amount = 2000m,
            Date = DateOnly.FromDateTime(DateTime.Today),
            RecordedAt = DateTime.UtcNow
        });

        group.Accounts.Add(account);
        _db.AccountGroups.Add(group);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetAccountsByGroupAsync(group.Id);

        // Assert
        result.Data!.First().CurrentBalance.ShouldBe(2000m);
    }

    [Fact]
    public async Task GetAccountsByGroupAsync_WithNoSnapshots_ShouldReturnZeroBalance()
    {
        // Arrange
        var group = new AccountGroup { Name = "Savings" };
        var account = new Account { Name = "Empty Account", AccountGroup = group };
        group.Accounts.Add(account);
        _db.AccountGroups.Add(group);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetAccountsByGroupAsync(group.Id);

        // Assert
        var accountResponse = result.Data!.First();
        accountResponse.CurrentBalance.ShouldBe(0m);
        accountResponse.HasSnapshots.ShouldBeFalse();
    }

    #endregion

    #region GetAccountByIdAsync Tests

    [Fact]
    public async Task GetAccountByIdAsync_WithNonExistentId_ShouldReturnNotFound()
    {
        // Act
        var result = await _sut.GetAccountByIdAsync(999);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Type.ShouldBe(ResultType.NotFound);
    }

    [Fact]
    public async Task GetAccountByIdAsync_WithExistingAccount_ShouldReturnAccount()
    {
        // Arrange
        var group = new AccountGroup { Name = "Savings" };
        var account = new Account { Name = "Main Account", Description = "Primary savings", AccountGroup = group };
        group.Accounts.Add(account);
        _db.AccountGroups.Add(group);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetAccountByIdAsync(account.Id);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Data.ShouldNotBeNull();
        result.Data.Name.ShouldBe("Main Account");
        result.Data.Description.ShouldBe("Primary savings");
    }

    [Fact]
    public async Task GetAccountByIdAsync_WithSnapshots_ShouldCalculateBalance()
    {
        // Arrange
        var group = new AccountGroup { Name = "Savings" };
        var account = new Account { Name = "Main Account", AccountGroup = group };
        account.BalanceSnapshots.Add(new BalanceSnapshot
        {
            Account = account,
            Amount = 3500.25m,
            Date = DateOnly.FromDateTime(DateTime.Today),
            RecordedAt = DateTime.UtcNow
        });
        group.Accounts.Add(account);
        _db.AccountGroups.Add(group);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetAccountByIdAsync(account.Id);

        // Assert
        result.Data!.CurrentBalance.ShouldBe(3500.25m);
        result.Data.HasSnapshots.ShouldBeTrue();
    }

    #endregion

    #region CreateAccountAsync Tests

    [Fact]
    public async Task CreateAccountAsync_WithNonExistentGroup_ShouldReturnNotFound()
    {
        // Act
        var result = await _sut.CreateAccountAsync(999, "Test Account", null);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Type.ShouldBe(ResultType.NotFound);
    }

    [Fact]
    public async Task CreateAccountAsync_WithValidData_ShouldCreateAccount()
    {
        // Arrange
        var group = new AccountGroup { Name = "Savings" };
        _db.AccountGroups.Add(group);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.CreateAccountAsync(group.Id, "Main Account", "Primary savings");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Type.ShouldBe(ResultType.Created);
        result.Data.ShouldNotBeNull();
        result.Data.Name.ShouldBe("Main Account");
        result.Data.Description.ShouldBe("Primary savings");
        result.Data.CurrentBalance.ShouldBe(0m);
        result.Data.HasSnapshots.ShouldBeFalse();
        result.Data.Type.ShouldBe(AccountType.Regular);
        result.Location.ShouldBe($"/api/accounts/{result.Data.Id}");

        // Verify in database
        var saved = await _db.Accounts.FirstOrDefaultAsync();
        saved.ShouldNotBeNull();
        saved.Name.ShouldBe("Main Account");
        saved.AccountGroupId.ShouldBe(group.Id);
    }

    [Fact]
    public async Task CreateAccountAsync_WithServicePensionType_ShouldCreatePensionAccount()
    {
        // Arrange
        var group = new AccountGroup { Name = "Pension" };
        _db.AccountGroups.Add(group);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.CreateAccountAsync(group.Id, "ITP2", null, AccountType.ServicePension);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Data.ShouldNotBeNull();
        result.Data.Type.ShouldBe(AccountType.ServicePension);

        var saved = await _db.Accounts.FirstOrDefaultAsync();
        saved.ShouldNotBeNull();
        saved.Type.ShouldBe(AccountType.ServicePension);
    }

    [Fact]
    public async Task CreateAccountAsync_WithoutDescription_ShouldCreateAccount()
    {
        // Arrange
        var group = new AccountGroup { Name = "Savings" };
        _db.AccountGroups.Add(group);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.CreateAccountAsync(group.Id, "Main Account", null);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Data!.Name.ShouldBe("Main Account");
        result.Data.Description.ShouldBeNull();
    }

    [Fact]
    public async Task CreateAccountAsync_WithWhitespace_ShouldTrimName()
    {
        // Arrange
        var group = new AccountGroup { Name = "Savings" };
        _db.AccountGroups.Add(group);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.CreateAccountAsync(group.Id, "  Main Account  ", "  Test  ");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Data!.Name.ShouldBe("Main Account");
        result.Data.Description.ShouldBe("Test");
    }

    [Fact]
    public async Task CreateAccountAsync_WithEmptyName_ShouldReturnBadRequest()
    {
        // Arrange
        var group = new AccountGroup { Name = "Savings" };
        _db.AccountGroups.Add(group);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.CreateAccountAsync(group.Id, "", null);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Type.ShouldBe(ResultType.BadRequest);
        result.ErrorMessage.ShouldBe("Name is required.");
    }

    [Fact]
    public async Task CreateAccountAsync_WithWhitespaceName_ShouldReturnBadRequest()
    {
        // Arrange
        var group = new AccountGroup { Name = "Savings" };
        _db.AccountGroups.Add(group);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.CreateAccountAsync(group.Id, "   ", null);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Type.ShouldBe(ResultType.BadRequest);
    }

    [Fact]
    public async Task CreateAccountAsync_WithDuplicateNameInSameGroup_ShouldReturnConflict()
    {
        // Arrange
        var group = new AccountGroup { Name = "Savings" };
        var existing = new Account { Name = "Main Account", AccountGroup = group };
        group.Accounts.Add(existing);
        _db.AccountGroups.Add(group);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.CreateAccountAsync(group.Id, "Main Account", null);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Type.ShouldBe(ResultType.Conflict);
        result.ErrorMessage.ShouldBe("An account with this name already exists in this group.");
    }

    [Fact]
    public async Task CreateAccountAsync_WithDuplicateNameDifferentCase_ShouldReturnConflict()
    {
        // Arrange
        var group = new AccountGroup { Name = "Savings" };
        var existing = new Account { Name = "Main Account", AccountGroup = group };
        group.Accounts.Add(existing);
        _db.AccountGroups.Add(group);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.CreateAccountAsync(group.Id, "MAIN ACCOUNT", null);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Type.ShouldBe(ResultType.Conflict);
    }

    [Fact]
    public async Task CreateAccountAsync_WithSameNameInDifferentGroup_ShouldSucceed()
    {
        // Arrange
        var group1 = new AccountGroup { Name = "Savings" };
        var group2 = new AccountGroup { Name = "Investments" };
        var existing = new Account { Name = "Main Account", AccountGroup = group1 };
        group1.Accounts.Add(existing);
        _db.AccountGroups.AddRange(group1, group2);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.CreateAccountAsync(group2.Id, "Main Account", null);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Data!.Name.ShouldBe("Main Account");
    }

    #endregion

    #region DeleteAccountAsync Tests

    [Fact]
    public async Task DeleteAccountAsync_WithExistingAccount_ShouldDelete()
    {
        // Arrange
        var group = new AccountGroup { Name = "Savings" };
        var account = new Account { Name = "Main Account", AccountGroup = group };
        group.Accounts.Add(account);
        _db.AccountGroups.Add(group);
        await _db.SaveChangesAsync();
        var accountId = account.Id;

        // Act
        var result = await _sut.DeleteAccountAsync(accountId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Type.ShouldBe(ResultType.NoContent);

        // Verify deletion
        var deleted = await _db.Accounts.FindAsync(accountId);
        deleted.ShouldBeNull();
    }

    [Fact]
    public async Task DeleteAccountAsync_WithNonExistentId_ShouldReturnNotFound()
    {
        // Act
        var result = await _sut.DeleteAccountAsync(999);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Type.ShouldBe(ResultType.NotFound);
    }

    [Fact]
    public async Task DeleteAccountAsync_WithSnapshots_ShouldDeleteCascade()
    {
        // Arrange
        var group = new AccountGroup { Name = "Savings" };
        var account = new Account { Name = "Main Account", AccountGroup = group };
        var snapshot = new BalanceSnapshot
        {
            Account = account,
            Amount = 1000m,
            Date = DateOnly.FromDateTime(DateTime.Today),
            RecordedAt = DateTime.UtcNow
        };
        account.BalanceSnapshots.Add(snapshot);
        group.Accounts.Add(account);
        _db.AccountGroups.Add(group);
        await _db.SaveChangesAsync();
        var accountId = account.Id;

        // Act
        var result = await _sut.DeleteAccountAsync(accountId);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        // Verify account and snapshots were deleted (cascade delete)
        var deletedAccount = await _db.Accounts.FindAsync(accountId);
        deletedAccount.ShouldBeNull();
    }

    #endregion
}
