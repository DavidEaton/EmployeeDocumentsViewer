namespace EmployeeDocumentsViewer.Features.Documents.Read.List;

public sealed record DocumentReadRow(
    int DocumentId,
    string Employee,
    string Department,
    string DocumentType,
    int Year);