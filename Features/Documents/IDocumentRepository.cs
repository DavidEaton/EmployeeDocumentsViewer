namespace EmployeeDocumentsViewer.Features.Documents;

using EmployeeDocumentsViewer.Features.Documents.List;

public interface IDocumentRepository
{
    Task<(int TotalCount, int FilteredCount, IReadOnlyList<DocumentReadRow> Items)> SearchAsync(
        Company company,
        int page,
        int size,
        IReadOnlyList<FilterDescriptor> filters,
        IReadOnlyList<SortDescriptor> sorters,
        CancellationToken cancellationToken);

    Task<BlobDocumentStream?> OpenReadAsync(
        Company company,
        string blobName,
        CancellationToken cancellationToken);
}
