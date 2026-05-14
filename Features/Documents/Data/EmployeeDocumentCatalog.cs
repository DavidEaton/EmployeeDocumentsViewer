namespace EmployeeDocumentsViewer.Features.Documents.Data;

public sealed class EmployeeDocumentCatalog
{
    public int EmployeeId { get; init; }
    public string EmployeeName { get; init; } = string.Empty;
    public string HomeDepartment { get; init; } = string.Empty;
    public DateTime? TerminationDate { get; init; }
    public string BlobName { get; init; } = string.Empty;
    public DateTime UpdatedUtc { get; init; }
    public bool IsDeleted { get; init; }
    public string DocumentTypeDisplay { get; init; } = string.Empty;
    public int Year { get; init; }
}
