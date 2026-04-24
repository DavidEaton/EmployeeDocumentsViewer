namespace EmployeeDocumentsViewer.Features.Documents.Data;

public sealed class EmployeeDocumentCatalog
{
    public long Id { get; init; }
    public string BlobName { get; init; } = string.Empty;
    public int EmployeeId { get; init; }
    public string DocumentTypeDisplay { get; init; } = string.Empty;
    public string? EmployeeName { get; init; }
    public string? HomeDepartment { get; init; }
    public bool EmployeeActive { get; init; }
    public DateTimeOffset? EmployeeLookupLastSyncedUtc { get; init; }
    public DateTimeOffset? UpdatedUtc { get; init; }
    public DateTimeOffset? BlobLastModifiedUtc { get; init; }
    public bool IsDeleted { get; init; }
}
