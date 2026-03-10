namespace EmployeeDocumentsViewer.Features.Documents;

public interface IDocumentRepository
{
    Task<(int TotalCount, int FilteredCount, IReadOnlyList<DocumentRecord> Items)> SearchAsync(
        string? searchTerm,
        string? sortColumn,
        string? sortDirection,
        int start,
        int length,
        CancellationToken cancellationToken);

    Task<DocumentRecord?> GetByIdAsync(int id, CancellationToken cancellationToken);
}