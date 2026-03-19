namespace EmployeeDocumentsViewer.Configuration;

public sealed class CompanyConnectionOptions
{
    public const string SectionName = "CompanyConnections";

    public Dictionary<string, CompanyConnectionItem> Companies { get; init; }
        = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class CompanyConnectionItem
{
    public string DisplayName { get; init; } = string.Empty;
    public string ConnectionString { get; init; } = string.Empty;
    public string BlobStorageConnectionString { get; init; } = string.Empty;
}
