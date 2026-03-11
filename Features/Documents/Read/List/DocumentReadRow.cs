namespace EmployeeDocumentsViewer.Features.Documents.Read.List;

public sealed record DocumentReadRow(
    string CompanyKey,
    int DocumentId,
    string Employee,
    string Department,
    string DocumentType,
    int Year);