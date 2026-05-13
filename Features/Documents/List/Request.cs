namespace EmployeeDocumentsViewer.Features.Documents.List;

public sealed class Request
{
    public string CompanyKey { get; init; } = string.Empty;
    public int Page { get; init; } = 1;
    public int Size { get; init; } = 50;
    public IReadOnlyList<SortDescriptor> Sorters { get; init; } = [];
    public IReadOnlyList<FilterDescriptor> Filters { get; init; } = [];
}

public sealed class SortDescriptor
{
    public string Field { get; init; } = string.Empty;
    public string Dir { get; init; } = "asc";
}

public sealed class FilterDescriptor
{
    public string Field { get; init; } = string.Empty;
    public string? Value { get; init; }
}
