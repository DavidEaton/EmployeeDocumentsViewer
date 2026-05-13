namespace EmployeeDocumentsViewer.Features.Documents.List;

public sealed class Response
{
    public int LastPage { get; init; }
    public int TotalCount { get; init; }
    public int FilteredCount { get; init; }
    public IReadOnlyList<DocumentReadRow> Data { get; init; } = [];
}
