namespace RoadToMillion.Api.Services;

public class ImportService(CsvImportService csvImportService, AppDbContext db) : IImportService
{
    public async Task<Result<ImportPreview>> ParsePreviewAsync(IFormFile file)
    {
        try
        {
            var preview = await csvImportService.ParsePreviewAsync(file);
            return Result<ImportPreview>.Success(preview);
        }
        catch (ImportValidationException ex)
        {
            return Result<ImportPreview>.BadRequest(ex.Message);
        }
    }

    public async Task<Result<ImportResult>> ExecuteImportAsync(ImportPreview preview)
    {
        // Validate that we have groups, accounts, and snapshots to import
        if (preview?.Groups == null || preview.Groups.Count == 0 ||
            preview.Groups.All(g => g.Accounts == null || g.Accounts.Count == 0 ||
                g.Accounts.All(a => a.Snapshots == null || a.Snapshots.Count == 0)))
        {
            return Result<ImportResult>.BadRequest("No valid records to import.");
        }

        // Server-side conflict check: any group not marked alreadyExists that now exists in DB?
        var newGroupNames = preview.Groups
            .Where(g => !g.AlreadyExists)
            .Select(g => g.Name.ToLower())
            .ToList();

        if (newGroupNames.Count > 0)
        {
            var nowExist = await db.AccountGroups
                .Where(g => newGroupNames.Contains(g.Name.ToLower()))
                .AnyAsync();

            if (nowExist)
            {
                return Result<ImportResult>.Conflict("Data changed since preview was generated. Please re-upload and preview again.");
            }
        }

        try
        {
            var result = await csvImportService.ExecuteImportAsync(preview);
            return Result<ImportResult>.Success(result);
        }
        catch (Exception ex)
        {
            return Result<ImportResult>.Error(ex.Message);
        }
    }
}