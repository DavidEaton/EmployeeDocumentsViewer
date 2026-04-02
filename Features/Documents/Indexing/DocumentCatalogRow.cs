namespace EmployeeDocumentsViewer.Features.Documents.Indexing;

public sealed record DocumentCatalogRow(
    string CompanyKey,
    string BlobName,
    byte[] BlobNameHash,
    int EmployeeId,
    string DocumentTypeToken,
    string DocumentTypeDisplay,
    DateTimeOffset? UpdatedUtc,
    DateTimeOffset? BlobLastModifiedUtc,
    string? ContentType,
    string? BlobETag,
    bool IsDeleted,
    DateTimeOffset LastIndexedUtc);