namespace RoadToMillion.Api.Services;

public interface IImportService
{
    Task<Result<ImportPreview>> ParsePreviewAsync(IFormFile file);

    Task<Result<ImportResult>> ExecuteImportAsync(ImportPreview preview);
}