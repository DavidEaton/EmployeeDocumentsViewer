namespace EmployeeDocumentsViewer.Features.Documents.Data.Entities;

public sealed class EmployeeDocumentCatalogEntity
{
    public long Id { get; init; }
    public string CompanyKey { get; init; } = string.Empty;
    public string BlobName { get; init; } = string.Empty;
    public int EmployeeId { get; init; }
    public string DocumentTypeDisplay { get; init; } = string.Empty;
    public DateTimeOffset? UpdatedUtc { get; init; }
    public DateTimeOffset? BlobLastModifiedUtc { get; init; }
    public bool IsDeleted { get; init; }
}
