using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using RoadToMillion.Api.Data;
using RoadToMillion.Api.Models;
using RoadToMillion.Api.Services;
using Shouldly;
using System.Text;

namespace RoadToMillion.UnitTests.Services;

/// <summary>
/// Unit tests for ImportService demonstrating CSV import orchestration logic.
/// </summary>
public class ImportServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly ICsvImportService _csvImportService;
    private readonly IImportService _sut;

    public ImportServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .EnableSensitiveDataLogging()
            .Options;

        _db = new AppDbContext(options);
        _csvImportService = Substitute.For<ICsvImportService>();
        _sut = new ImportService(_csvImportService, _db);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    #region ParsePreviewAsync Tests

    [Fact]
    public async Task ParsePreviewAsync_WithValidFile_ShouldReturnSuccess()
    {
        // Arrange
        var file = CreateMockFile("test.csv");
        var expectedPreview = new ImportPreview(
            new List<ImportGroupPreview>
            {
                new ImportGroupPreview("Savings", false, new List<ImportAccountPreview>
                {
                    new ImportAccountPreview("Account 1", false, new List<ImportSnapshotPreview>
                    {
                        new ImportSnapshotPreview(1000m, DateTime.UtcNow)
                    })
                })
            },
            new List<ImportWarning>(),
            1, 1, 0
        );

        _csvImportService.ParsePreviewAsync(file).Returns(Task.FromResult(expectedPreview));

        // Act
        var result = await _sut.ParsePreviewAsync(file);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Type.ShouldBe(ResultType.Success);
        result.Data.ShouldBe(expectedPreview);
        await _csvImportService.Received(1).ParsePreviewAsync(file);
    }

    [Fact]
    public async Task ParsePreviewAsync_WithInvalidFile_ShouldReturnBadRequest()
    {
        // Arrange
        var file = CreateMockFile("test.csv");
        _csvImportService.ParsePreviewAsync(file)
            .Returns<ImportPreview>(x => throw new ImportValidationException("Invalid file format"));

        // Act
        var result = await _sut.ParsePreviewAsync(file);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Type.ShouldBe(ResultType.BadRequest);
        result.ErrorMessage.ShouldBe("Invalid file format");
    }

    [Fact]
    public async Task ParsePreviewAsync_WithMissingColumns_ShouldReturnBadRequest()
    {
        // Arrange
        var file = CreateMockFile("test.csv");
        _csvImportService.ParsePreviewAsync(file)
            .Returns<ImportPreview>(x => throw new ImportValidationException("Required columns are missing: AccountName"));

        // Act
        var result = await _sut.ParsePreviewAsync(file);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Type.ShouldBe(ResultType.BadRequest);
        result.ErrorMessage.ShouldContain("Required columns are missing");
    }

    #endregion

    #region ExecuteImportAsync Tests

    [Fact]
    public async Task ExecuteImportAsync_WithNullPreview_ShouldReturnBadRequest()
    {
        // Act
        var result = await _sut.ExecuteImportAsync(null!);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Type.ShouldBe(ResultType.BadRequest);
        result.ErrorMessage.ShouldBe("No valid records to import.");
    }

    [Fact]
    public async Task ExecuteImportAsync_WithEmptyGroups_ShouldReturnBadRequest()
    {
        // Arrange
        var preview = new ImportPreview(
            new List<ImportGroupPreview>(),
            new List<ImportWarning>(),
            0, 0, 0
        );

        // Act
        var result = await _sut.ExecuteImportAsync(preview);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Type.ShouldBe(ResultType.BadRequest);
        result.ErrorMessage.ShouldBe("No valid records to import.");
    }

    [Fact]
    public async Task ExecuteImportAsync_WithGroupsButNoAccounts_ShouldReturnBadRequest()
    {
        // Arrange
        var preview = new ImportPreview(
            new List<ImportGroupPreview>
            {
                new ImportGroupPreview("Savings", false, new List<ImportAccountPreview>())
            },
            new List<ImportWarning>(),
            0, 0, 0
        );

        // Act
        var result = await _sut.ExecuteImportAsync(preview);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Type.ShouldBe(ResultType.BadRequest);
    }

    [Fact]
    public async Task ExecuteImportAsync_WithAccountsButNoSnapshots_ShouldReturnBadRequest()
    {
        // Arrange
        var preview = new ImportPreview(
            new List<ImportGroupPreview>
            {
                new ImportGroupPreview("Savings", false, new List<ImportAccountPreview>
                {
                    new ImportAccountPreview("Account 1", false, new List<ImportSnapshotPreview>())
                })
            },
            new List<ImportWarning>(),
            0, 0, 0
        );

        // Act
        var result = await _sut.ExecuteImportAsync(preview);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Type.ShouldBe(ResultType.BadRequest);
    }

    [Fact]
    public async Task ExecuteImportAsync_WithNewGroupThatNowExists_ShouldReturnConflict()
    {
        // Arrange
        // Create a group in the database
        var existingGroup = new AccountGroup { Name = "Savings" };
        _db.AccountGroups.Add(existingGroup);
        await _db.SaveChangesAsync();

        // Create a preview that thinks the group doesn't exist
        var preview = new ImportPreview(
            new List<ImportGroupPreview>
            {
                new ImportGroupPreview("Savings", false, new List<ImportAccountPreview>
                {
                    new ImportAccountPreview("Account 1", false, new List<ImportSnapshotPreview>
                    {
                        new ImportSnapshotPreview(1000m, DateTime.UtcNow)
                    })
                })
            },
            new List<ImportWarning>(),
            1, 1, 0
        );

        // Act
        var result = await _sut.ExecuteImportAsync(preview);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Type.ShouldBe(ResultType.Conflict);
        result.ErrorMessage.ShouldBe("Data changed since preview was generated. Please re-upload and preview again.");
    }

    [Fact]
    public async Task ExecuteImportAsync_WithNewGroupCaseInsensitive_ShouldReturnConflict()
    {
        // Arrange
        var existingGroup = new AccountGroup { Name = "savings" };
        _db.AccountGroups.Add(existingGroup);
        await _db.SaveChangesAsync();

        var preview = new ImportPreview(
            new List<ImportGroupPreview>
            {
                new ImportGroupPreview("SAVINGS", false, new List<ImportAccountPreview>
                {
                    new ImportAccountPreview("Account 1", false, new List<ImportSnapshotPreview>
                    {
                        new ImportSnapshotPreview(1000m, DateTime.UtcNow)
                    })
                })
            },
            new List<ImportWarning>(),
            1, 1, 0
        );

        // Act
        var result = await _sut.ExecuteImportAsync(preview);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Type.ShouldBe(ResultType.Conflict);
    }

    [Fact]
    public async Task ExecuteImportAsync_WithExistingGroupMarkedAsExists_ShouldNotCheckConflict()
    {
        // Arrange
        var existingGroup = new AccountGroup { Name = "Savings" };
        _db.AccountGroups.Add(existingGroup);
        await _db.SaveChangesAsync();

        var preview = new ImportPreview(
            new List<ImportGroupPreview>
            {
                new ImportGroupPreview("Savings", true, new List<ImportAccountPreview>
                {
                    new ImportAccountPreview("Account 1", false, new List<ImportSnapshotPreview>
                    {
                        new ImportSnapshotPreview(1000m, DateTime.UtcNow)
                    })
                })
            },
            new List<ImportWarning>(),
            1, 1, 0
        );

        var importResult = new ImportResult(0, 1, 1, 0, new List<ImportWarning>(), DateTime.UtcNow);
        _csvImportService.ExecuteImportAsync(preview).Returns(Task.FromResult(importResult));

        // Act
        var result = await _sut.ExecuteImportAsync(preview);

        // Assert
        result.IsSuccess.ShouldBeTrue(); // Should not conflict since group is marked as already exists
        result.Type.ShouldBe(ResultType.Success);
    }

    [Fact]
    public async Task ExecuteImportAsync_WithValidPreview_ShouldCallCsvImportService()
    {
        // Arrange
        var preview = new ImportPreview(
            new List<ImportGroupPreview>
            {
                new ImportGroupPreview("Savings", false, new List<ImportAccountPreview>
                {
                    new ImportAccountPreview("Account 1", false, new List<ImportSnapshotPreview>
                    {
                        new ImportSnapshotPreview(1000m, DateTime.UtcNow)
                    })
                })
            },
            new List<ImportWarning>(),
            1, 1, 0
        );

        var importResult = new ImportResult(1, 1, 1, 0, new List<ImportWarning>(), DateTime.UtcNow);
        _csvImportService.ExecuteImportAsync(preview).Returns(Task.FromResult(importResult));

        // Act
        var result = await _sut.ExecuteImportAsync(preview);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Type.ShouldBe(ResultType.Success);
        result.Data.ShouldBe(importResult);
        await _csvImportService.Received(1).ExecuteImportAsync(preview);
    }

    [Fact]
    public async Task ExecuteImportAsync_WhenCsvServiceThrows_ShouldReturnError()
    {
        // Arrange
        var preview = new ImportPreview(
            new List<ImportGroupPreview>
            {
                new ImportGroupPreview("Savings", false, new List<ImportAccountPreview>
                {
                    new ImportAccountPreview("Account 1", false, new List<ImportSnapshotPreview>
                    {
                        new ImportSnapshotPreview(1000m, DateTime.UtcNow)
                    })
                })
            },
            new List<ImportWarning>(),
            1, 1, 0
        );

        _csvImportService.ExecuteImportAsync(preview)
            .Returns<ImportResult>(x => throw new Exception("Database error"));

        // Act
        var result = await _sut.ExecuteImportAsync(preview);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Type.ShouldBe(ResultType.Error);
        result.ErrorMessage.ShouldBe("Database error");
    }

    [Fact]
    public async Task ExecuteImportAsync_WithMultipleNewGroups_ShouldCheckAllForConflicts()
    {
        // Arrange
        // Only add one of the groups to the database
        var existingGroup = new AccountGroup { Name = "Savings" };
        _db.AccountGroups.Add(existingGroup);
        await _db.SaveChangesAsync();

        var preview = new ImportPreview(
            new List<ImportGroupPreview>
            {
                new ImportGroupPreview("Savings", false, new List<ImportAccountPreview>
                {
                    new ImportAccountPreview("Account 1", false, new List<ImportSnapshotPreview>
                    {
                        new ImportSnapshotPreview(1000m, DateTime.UtcNow)
                    })
                }),
                new ImportGroupPreview("Investments", false, new List<ImportAccountPreview>
                {
                    new ImportAccountPreview("Account 2", false, new List<ImportSnapshotPreview>
                    {
                        new ImportSnapshotPreview(2000m, DateTime.UtcNow)
                    })
                })
            },
            new List<ImportWarning>(),
            2, 2, 0
        );

        // Act
        var result = await _sut.ExecuteImportAsync(preview);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Type.ShouldBe(ResultType.Conflict);
    }

    #endregion

    private static IFormFile CreateMockFile(string fileName)
    {
        var file = Substitute.For<IFormFile>();
        file.FileName.Returns(fileName);
        file.ContentType.Returns("text/csv");
        file.Length.Returns(100);
        file.OpenReadStream().Returns(new MemoryStream(Encoding.UTF8.GetBytes("test content")));
        return file;
    }
}
