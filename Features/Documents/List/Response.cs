namespace EmployeeDocumentsViewer.Features.Documents.List;

public sealed class Response
{
    [JsonPropertyName("last_page")]
    public int LastPage { get; init; }

    [JsonPropertyName("total_count")]
    public int TotalCount { get; init; }

    [JsonPropertyName("filtered_count")]
    public int FilteredCount { get; init; }

    [JsonPropertyName("data")]
    public IReadOnlyList<DocumentReadRow> Data { get; init; } = [];
}
