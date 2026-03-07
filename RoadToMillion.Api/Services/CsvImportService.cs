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

        // Load existing data for conflict detection
        var existingGroups = await db.AccountGroups
            .Include(g => g.Accounts)
            .ToDictionaryAsync(g => g.Name.ToLower(), g => g);

        // Parse rows
        var groupDict = new Dictionary<string, (bool AlreadyExists, Dictionary<string, (decimal? Balance, bool AlreadyExists)> Accounts)>(StringComparer.OrdinalIgnoreCase);
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

            if (!groupDict.TryGetValue(groupName, out var groupEntry))
            {
                var groupAlreadyExists = existingGroups.ContainsKey(groupName.ToLower());
                groupDict[groupName] = groupEntry = (groupAlreadyExists, new Dictionary<string, (decimal?, bool)>(StringComparer.OrdinalIgnoreCase));
            }

            if (groupEntry.Accounts.ContainsKey(accountName))
            {
                warnings.Add(new ImportWarning(rowNumber, "AccountName",
                    $"Account '{accountName}' appears more than once in group '{groupName}' in the file; duplicate row will be skipped.", "Warning"));
                rowsSkipped++;
                continue;
            }

            // Check if account already exists in DB
            bool accountAlreadyExists = false;
            if (existingGroups.TryGetValue(groupName.ToLower(), out var existingGroup))
                accountAlreadyExists = existingGroup.Accounts.Any(a => a.Name.Equals(accountName, StringComparison.OrdinalIgnoreCase));

            if (accountAlreadyExists)
            {
                warnings.Add(new ImportWarning(null, null,
                    $"Account '{accountName}' already exists in group '{groupName}' and will be skipped.", "Info"));
                groupEntry.Accounts[accountName] = (balance, true);
                rowsSkipped++;
            }
            else
            {
                groupEntry.Accounts[accountName] = (balance, false);
            }
        }

        if (rowsTotal == 0)
            throw new ImportValidationException("The file is empty or contains no data rows.");

        // Build preview
        var groups = groupDict.Select(kvp =>
        {
            var accounts = kvp.Value.Accounts.Select(a =>
                new ImportAccountPreview(a.Key, a.Value.Balance, a.Value.AlreadyExists, a.Value.AlreadyExists))
                .ToList();
            return new ImportGroupPreview(kvp.Key, kvp.Value.AlreadyExists, accounts);
        }).ToList();

        var rowsValid = rowsTotal - rowsSkipped;
        return new ImportPreview(groups, warnings, rowsTotal, rowsValid, rowsSkipped);
    }

    public async Task<ImportResult> ExecuteImportAsync(ImportPreview preview)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            var now = DateTime.UtcNow;
            var today = DateOnly.FromDateTime(now);
            int groupsCreated = 0, accountsCreated = 0, snapshotsCreated = 0, rowsSkipped = 0;
            var skipReasons = new List<ImportWarning>();

            // Load existing groups once
            var existingGroups = await db.AccountGroups
                .Include(g => g.Accounts)
                .ToDictionaryAsync(g => g.Name.ToLower(), g => g);

            foreach (var groupPreview in preview.Groups)
            {
                Models.AccountGroup group;
                if (existingGroups.TryGetValue(groupPreview.Name.ToLower(), out var existingGroup))
                {
                    group = existingGroup;
                }
                else
                {
                    group = new Models.AccountGroup { Name = groupPreview.Name };
                    db.AccountGroups.Add(group);
                    await db.SaveChangesAsync(); // flush to get Id
                    groupsCreated++;
                    existingGroup = group;
                    existingGroups[group.Name.ToLower()] = group;
                }

                foreach (var accountPreview in groupPreview.Accounts)
                {
                    if (accountPreview.WillBeSkipped)
                    {
                        rowsSkipped++;
                        skipReasons.Add(new ImportWarning(null, null,
                            $"Account '{accountPreview.Name}' in group '{groupPreview.Name}' was skipped.", "Info"));
                        continue;
                    }

                    // Double-check in DB (conflict guard)
                    var accountExists = existingGroup.Accounts
                        .Any(a => a.Name.Equals(accountPreview.Name, StringComparison.OrdinalIgnoreCase));
                    if (accountExists)
                    {
                        rowsSkipped++;
                        skipReasons.Add(new ImportWarning(null, null,
                            $"Account '{accountPreview.Name}' already exists in group '{groupPreview.Name}' and was skipped.", "Info"));
                        continue;
                    }

                    var account = new Models.Account
                    {
                        AccountGroupId = group.Id,
                        Name = accountPreview.Name
                    };
                    db.Accounts.Add(account);
                    await db.SaveChangesAsync();
                    accountsCreated++;

                    if (accountPreview.InitialBalance is { } bal && bal > 0)
                    {
                        db.BalanceSnapshots.Add(new Models.BalanceSnapshot
                        {
                            AccountId = account.Id,
                            Amount = bal,
                            Date = today,
                            RecordedAt = now
                        });
                        await db.SaveChangesAsync();
                        snapshotsCreated++;
                    }
                }
            }

            await transaction.CommitAsync();
            return new ImportResult(groupsCreated, accountsCreated, snapshotsCreated, rowsSkipped, skipReasons, now);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private static string DetectDelimiter(string firstLine)
    {
        int commas = CountOutsideQuotes(firstLine, ',');
        int semis = CountOutsideQuotes(firstLine, ';');
        int tabs = CountOutsideQuotes(firstLine, '\t');

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

public class ImportValidationException(string message) : Exception(message);
