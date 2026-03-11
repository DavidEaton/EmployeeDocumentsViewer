namespace EmployeeDocumentsViewer.Features.Documents;

public class SqlDocumentRepository : IDocumentRepository
{
    public Task<DocumentRecord?> GetByIdAsync(
        Company company,
        int id,
        CancellationToken cancellationToken) =>
            throw new NotImplementedException();

    public Task<(int TotalCount, int FilteredCount, IReadOnlyList<DocumentRecord> Items)> SearchAsync(
        Company company,
        string? searchTerm,
        string? sortColumn,
        string? sortDirection,
        int start,
        int length,
        CancellationToken cancellationToken)
        => throw new NotImplementedException();
}