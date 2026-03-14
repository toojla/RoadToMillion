namespace RoadToMillion.Api.Services;

public interface ICsvImportService
{
    Task<ImportPreview> ParsePreviewAsync(IFormFile file);
    Task<ImportResult> ExecuteImportAsync(ImportPreview preview);
}
