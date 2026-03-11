namespace EmployeeDocumentsViewer.Features.Documents.Read.List;

internal static class Mappings
{
    public static DocumentReadRow ToReadRow(
        this DocumentRecord document,
        string companyKey) =>
        new(
            companyKey,
            document.Id,
            document.Employee,
            document.Department,
            document.DocumentType,
            document.Year);
}