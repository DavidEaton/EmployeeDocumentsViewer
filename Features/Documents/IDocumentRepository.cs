namespace EmployeeDocumentsViewer.Features.Documents;

using EmployeeDocumentsViewer.Features.Documents.List;

public interface IDocumentRepository
{
    Task<(int TotalCount, int FilteredCount, IReadOnlyList<DocumentReadRow> Items)> SearchAsync(
        Company company,
        string? searchTerm,
        DocumentSortColumn sortColumn,
        bool descending,
        int start,
        int length,
        CancellationToken cancellationToken);

    Task<BlobDocumentStream?> OpenReadAsync(
        Company company,
        string blobName,
        CancellationToken cancellationToken);
}