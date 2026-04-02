namespace EmployeeDocumentsViewer.Features.Documents;

public static class DocumentSortParser
{
    public static DocumentSortColumn ParseOrDefault(string? sortColumn)
    {
        if (string.IsNullOrWhiteSpace(sortColumn))
            return DocumentSortColumn.UpdatedUtc;

        return sortColumn.Trim().ToLowerInvariant() switch
        {
            "employeeid" => DocumentSortColumn.EmployeeId,
            "employee" => DocumentSortColumn.Employee,
            "department" => DocumentSortColumn.Department,
            "documenttype" => DocumentSortColumn.DocumentType,
            "year" => DocumentSortColumn.Year,
            "active" => DocumentSortColumn.Active,
            "terminationdate" => DocumentSortColumn.TerminationDate,
            "updatedutc" => DocumentSortColumn.UpdatedUtc,
            _ => DocumentSortColumn.UpdatedUtc
        };
    }

    public static bool IsDescending(string? sortDirection) =>
        string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);
}