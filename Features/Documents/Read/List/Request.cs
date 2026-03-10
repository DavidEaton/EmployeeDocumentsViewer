namespace EmployeeDocumentsViewer.Features.Documents.Read.List;

public sealed class Request
{
    public int Draw { get; init; }
    public int Start { get; init; }
    public int Length { get; init; } = 10;
    public string? SearchTerm { get; init; }
    public string? SortColumn { get; init; }
    public string? SortDirection { get; init; }
}