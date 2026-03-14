using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using RoadToMillion.Api.Data;
using RoadToMillion.Api.Models;

namespace RoadToMillion.Api.Services;

public class CsvImportService(AppDbContext db)
{
    private static readonly string[] RequiredColumns = ["AccountGroup", "AccountName"];

    public async Task<ImportPreview> ParsePreviewAsync(IFormFile file)
    {
        // File-level validation: type check
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var ct = file.ContentType.ToLowerInvariant();
        if (ext != ".csv" && ct != "text/csv" && ct != "text/plain" && ct != "application/csv")
            throw new ImportValidationException("File type not supported. Please upload a CSV file.");

        // Copy to MemoryStream (IFormFile stream is non-seekable)
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        ms.Position = 0;

        using var reader = new StreamReader(ms);
        var firstLine = await reader.ReadLineAsync() ?? string.Empty;

        // Auto-detect delimiter
        var delimiter = DetectDelimiter(firstLine);

        // Re-read from start
        ms.Position = 0;
        using var reader2 = new StreamReader(ms);

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = delimiter,
            HasHeaderRecord = true,
            MissingFieldFound = null,
            BadDataFound = null,
            HeaderValidated = null,
        };

        using var csv = new CsvReader(reader2, config);

        // Read header
        await csv.ReadAsync();
        csv.ReadHeader();
        var headers = csv.HeaderRecord ?? [];

        // Validate required columns (case-insensitive)
        var headerSet = headers.Select(h => h.Trim().ToLower()).ToHashSet();
        var missingColumns = RequiredColumns
            .Where(c => !headerSet.Contains(c.ToLower()))
            .ToList();

        if (missingColumns.Count > 0)
            throw new ImportValidationException($"Required columns are missing: {string.Join(", ", missingColumns)}");

        // Map column names (case-insensitive)
        string? FindHeader(string name) =>
            headers.FirstOrDefault(h => h.Trim().Equals(name, StringComparison.OrdinalIgnoreCase));

        var groupCol = FindHeader("AccountGroup")!;
        var nameCol = FindHeader("AccountName")!;
        var balanceCol = FindHeader("Balance");
        var dateCol = FindHeader("Date");

        // Load existing data for conflict detection
        var existingGroups = await db.AccountGroups
            .Include(g => g.Accounts)
            .ToDictionaryAsync(g => g.Name.ToLower(), g => g);

        // Parse rows - now tracking snapshots instead of just accounts
        var groupDict = new Dictionary<string, (bool AlreadyExists, Dictionary<string, AccountImportData> Accounts)>(StringComparer.OrdinalIgnoreCase);
        var warnings = new List<ImportWarning>();
        int rowNumber = 0;
        int rowsTotal = 0;
        int rowsSkipped = 0;

        while (await csv.ReadAsync())
        {
            rowNumber++;
            rowsTotal++;

            var groupName = csv.GetField(groupCol)?.Trim() ?? string.Empty;
            var accountName = csv.GetField(nameCol)?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(groupName))
            {
                warnings.Add(new ImportWarning(rowNumber, "AccountGroup", "AccountGroup is missing; row will be skipped.", "Warning"));
                rowsSkipped++;
                continue;
            }

            if (string.IsNullOrWhiteSpace(accountName))
            {
                warnings.Add(new ImportWarning(rowNumber, "AccountName", "AccountName is missing; row will be skipped.", "Warning"));
                rowsSkipped++;
                continue;
            }

            // Parse optional balance
            decimal? balance = null;
            if (balanceCol is not null)
            {
                var raw = csv.GetField(balanceCol)?.Trim();
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var b) ||
                        decimal.TryParse(raw, NumberStyles.Any, new CultureInfo("sv-SE"), out b))
                    {
                        if (b > 0) balance = b;
                    }
                }
            }

            // Parse optional date
            DateTime? snapshotDate = null;
            if (dateCol is not null)
            {
                var raw = csv.GetField(dateCol)?.Trim();
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    // Try multiple date formats and convert to UTC
                    // Use AssumeUniversal | AdjustToUniversal to treat parsed dates as UTC
                    if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var d) ||
                        DateTime.TryParse(raw, new CultureInfo("sv-SE"), DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out d))
                    {
                        // Ensure the DateTime is UTC (in case parsing didn't set it)
                        snapshotDate = DateTime.SpecifyKind(d, DateTimeKind.Utc);
                    }
                    else
                    {
                        warnings.Add(new ImportWarning(rowNumber, "Date",
                            $"Invalid date format '{raw}'; will use current date/time instead.", "Warning"));
                    }
                }
            }

            // Skip rows without balance
            if (balance is null || balance <= 0)
            {
                warnings.Add(new ImportWarning(rowNumber, "Balance",
                    "Balance is missing or zero; row will be skipped.", "Warning"));
                rowsSkipped++;
                continue;
            }

            if (!groupDict.TryGetValue(groupName, out var groupEntry))
            {
                var groupAlreadyExists = existingGroups.ContainsKey(groupName.ToLower());
                groupDict[groupName] = groupEntry = (groupAlreadyExists, new Dictionary<string, AccountImportData>(StringComparer.OrdinalIgnoreCase));
            }

            // Check if account already exists in DB
            bool accountAlreadyExists = false;
            if (existingGroups.TryGetValue(groupName.ToLower(), out var existingGroup))
                accountAlreadyExists = existingGroup.Accounts.Any(a => a.Name.Equals(accountName, StringComparison.OrdinalIgnoreCase));

            // Add or update account entry with snapshot
            if (!groupEntry.Accounts.TryGetValue(accountName, out var accountData))
            {
                accountData = new AccountImportData(accountName, accountAlreadyExists);
                groupEntry.Accounts[accountName] = accountData;
            }

            accountData.Snapshots.Add(new SnapshotImportData(balance.Value, snapshotDate));
        }

        if (rowsTotal == 0)
            throw new ImportValidationException("The file is empty or contains no data rows.");

        // Build preview
        var groups = groupDict.Select(kvp =>
        {
            var accounts = kvp.Value.Accounts.Select(a =>
                new ImportAccountPreview(
                    a.Key,
                    a.Value.AlreadyExists,
                    a.Value.Snapshots.Select(s => new ImportSnapshotPreview(s.Amount, s.SnapshotDate)).ToList()
                )).ToList();
            return new ImportGroupPreview(kvp.Key, kvp.Value.AlreadyExists, accounts);
        }).ToList();

        var rowsValid = rowsTotal - rowsSkipped;
        return new ImportPreview(groups, warnings, rowsTotal, rowsValid, rowsSkipped);
    }

    public async Task<ImportResult> ExecuteImportAsync(ImportPreview preview)
    {
        var strategy = db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            try
            {
                var now = DateTime.UtcNow;
                int groupsCreated = 0, accountsCreated = 0, snapshotsCreated = 0;
                var skipReasons = new List<ImportWarning>();

                // Load existing groups once
                var existingGroups = await db.AccountGroups
                    .Include(g => g.Accounts)
                    .ToDictionaryAsync(g => g.Name.ToLower(), g => g);

                foreach (var groupPreview in preview.Groups)
                {
                    Models.AccountGroup group;
                    Dictionary<string, Models.Account> accountDict;

                    if (existingGroups.TryGetValue(groupPreview.Name.ToLower(), out var existingGroup))
                    {
                        group = existingGroup;
                        accountDict = existingGroup.Accounts.ToDictionary(a => a.Name.ToLower(), a => a, StringComparer.OrdinalIgnoreCase);
                    }
                    else
                    {
                        group = new Models.AccountGroup { Name = groupPreview.Name };
                        db.AccountGroups.Add(group);
                        await db.SaveChangesAsync(); // flush to get Id
                        groupsCreated++;
                        accountDict = new Dictionary<string, Models.Account>(StringComparer.OrdinalIgnoreCase);
                    }

                    foreach (var accountPreview in groupPreview.Accounts)
                    {
                        Models.Account account;

                        if (accountDict.TryGetValue(accountPreview.Name, out var existingAccount))
                        {
                            // Account exists - we'll add snapshots to it
                            account = existingAccount;
                        }
                        else
                        {
                            // Create new account
                            account = new Models.Account
                            {
                                AccountGroupId = group.Id,
                                Name = accountPreview.Name
                            };
                            db.Accounts.Add(account);
                            await db.SaveChangesAsync();
                            accountsCreated++;
                            accountDict[accountPreview.Name] = account;
                        }

                        // Add all snapshots for this account
                        foreach (var snapshotPreview in accountPreview.Snapshots)
                        {
                            var snapshotDateTime = snapshotPreview.SnapshotDate ?? now;
                            var snapshotDateOnly = DateOnly.FromDateTime(snapshotDateTime);

                            db.BalanceSnapshots.Add(new Models.BalanceSnapshot
                            {
                                AccountId = account.Id,
                                Amount = snapshotPreview.Amount,
                                Date = snapshotDateOnly,
                                RecordedAt = snapshotDateTime
                            });
                            snapshotsCreated++;
                        }

                        if (accountPreview.Snapshots.Count > 0)
                        {
                            await db.SaveChangesAsync();
                        }
                    }
                }

                await transaction.CommitAsync();
                return new ImportResult(groupsCreated, accountsCreated, snapshotsCreated, 0, skipReasons, now);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
    }

    private static string DetectDelimiter(string firstLine)
    {
        var commas = CountOutsideQuotes(firstLine, ',');
        var semis = CountOutsideQuotes(firstLine, ';');
        var tabs = CountOutsideQuotes(firstLine, '\t');

        if (tabs >= commas && tabs >= semis) return "\t";
        if (semis >= commas) return ";";
        return ",";
    }

    private static int CountOutsideQuotes(string line, char ch)
    {
        int count = 0;
        bool inQuotes = false;
        foreach (var c in line)
        {
            if (c == '"') inQuotes = !inQuotes;
            else if (!inQuotes && c == ch) count++;
        }
        return count;
    }
}

// Helper classes for tracking import data
internal class AccountImportData(string name, bool alreadyExists)
{
    public string Name { get; } = name;
    public bool AlreadyExists { get; } = alreadyExists;
    public List<SnapshotImportData> Snapshots { get; } = new();
}

internal record SnapshotImportData(decimal Amount, DateTime? SnapshotDate);

public class ImportValidationException(string message) : Exception(message);