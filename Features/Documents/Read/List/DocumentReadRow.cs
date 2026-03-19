namespace EmployeeDocumentsViewer.Features.Documents.Read.List;

public sealed record DocumentReadRow(
    string CompanyKey,
    string BlobName,
    int EmployeeId,
    string Employee,
    string Department,
    string DocumentType,
    int Year,
    string? UpdatedUtc);