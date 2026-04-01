namespace EmployeeDocumentsViewer.Features.Documents;

public sealed record DocumentRecord(
    string BlobName,
    int EmployeeId,
    string Employee,
    string Department,
    string DocumentType,
    int Year,
    DateTimeOffset? UpdatedUtc,
    bool Active,
    DateTime? TerminationDate,
    string? ContentType);