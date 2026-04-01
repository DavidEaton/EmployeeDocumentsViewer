namespace EmployeeDocumentsViewer.Features.Documents.List;

public sealed class Response
{
    public int Draw { get; init; }
    public int RecordsTotal { get; init; }
    public int RecordsFiltered { get; init; }
    public IReadOnlyList<DocumentReadRow> Data { get; init; } = [];
}