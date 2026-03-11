namespace EmployeeDocumentsViewer.Features.Documents;

public interface IDocumentRepository
{
    Task<(int TotalCount, int FilteredCount, IReadOnlyList<DocumentRecord> Items)> SearchAsync(
        Company company,
        string? searchTerm,
        string? sortColumn,
        string? sortDirection,
        int start,
        int length,
        CancellationToken cancellationToken);

    Task<DocumentRecord?> GetByIdAsync(
        Company company,
        int id,
        CancellationToken cancellationToken);
}