namespace EmployeeDocumentsViewer.Features.Documents.List;

public sealed record DocumentReadRow(
    string BlobName,
    int EmployeeId,
    string Employee,
    string Department,
    string DocumentType,
    int? Year,
    bool Active,
    DateTime? TerminationDate,
    DateTimeOffset? UpdatedUtc);