namespace EmployeeDocumentsViewer.Configuration;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public string DocumentsContainerName { get; init; } = "hrdocs";
    public string PicturesContainerName { get; init; } = "eepics";
}
