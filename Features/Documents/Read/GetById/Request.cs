namespace EmployeeDocumentsViewer.Features.Documents.Read.GetById;

public sealed class Request
{
    public string CompanyKey { get; init; } = string.Empty;
    public int Id { get; init; }
}