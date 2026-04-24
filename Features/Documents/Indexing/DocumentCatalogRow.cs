namespace EmployeeDocumentsViewer.Features.Documents.Indexing;

public sealed record DocumentCatalogRow(
    string BlobName,
    byte[] BlobNameHash,
    int EmployeeId,
    string DocumentTypeToken,
    string DocumentTypeDisplay,
    string? EmployeeName,
    string? HomeDepartment,
    bool EmployeeActive,
    DateTimeOffset? EmployeeLookupLastSyncedUtc,
    DateTimeOffset? UpdatedUtc,
    DateTimeOffset? BlobLastModifiedUtc,
    string? ContentType,
    string? BlobETag,
    bool IsDeleted,
    DateTimeOffset LastIndexedUtc);
