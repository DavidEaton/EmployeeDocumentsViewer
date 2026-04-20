namespace EmployeeDocumentsViewer.Features.Documents.Data.Entities;

public sealed class EmployeeDocumentsLookupEntity
{
    public int Id { get; init; }
    public string? NameLastFirst { get; init; }
    public string? HomeDepartment { get; init; }
    public bool Active { get; init; }
}
