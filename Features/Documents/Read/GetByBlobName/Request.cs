namespace EmployeeDocumentsViewer.Features.Documents.Read.GetByBlobName;

public sealed class Request
{
    public string CompanyKey { get; init; } = string.Empty;
    public string BlobName { get; init; } = string.Empty;
}