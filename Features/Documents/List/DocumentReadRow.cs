namespace EmployeeDocumentsViewer.Features.Documents.List;

public sealed record DocumentReadRow(
    string BlobName,
    int EmployeeId,
    string EmployeeName,
    string Department,
    string DocumentType,
    int? Year,
    DateTime? TerminationDate,
    DateTimeOffset? UpdatedUtc);
