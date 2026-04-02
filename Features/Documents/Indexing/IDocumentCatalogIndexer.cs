namespace EmployeeDocumentsViewer.Features.Documents.Indexing;

public interface IDocumentCatalogIndexer
{
    Task SyncCompanyAsync(Company company, CancellationToken cancellationToken);
}