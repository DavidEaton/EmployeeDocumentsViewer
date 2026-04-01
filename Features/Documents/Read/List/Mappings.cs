namespace EmployeeDocumentsViewer.Features.Documents.Read.List;

internal static class Mappings
{
    public static DocumentReadRow ToReadRow(
        this DocumentRecord document,
        string companyKey) =>
        new(
            companyKey,
            document.BlobName,
            document.EmployeeId,
            document.Employee,
            document.Department,
            document.DocumentType,
            document.Year,
            document.Active,
            document.TerminationDate,
            document.UpdatedUtc?.UtcDateTime.ToString("O"));
}