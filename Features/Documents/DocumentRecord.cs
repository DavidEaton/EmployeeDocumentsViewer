namespace EmployeeDocumentsViewer.Features.Documents;

public sealed record DocumentRecord(
    int Id,
    string Employee,
    string Department,
    string DocumentType,
    int Year,
    string FileName,
    byte[] PdfBytes);