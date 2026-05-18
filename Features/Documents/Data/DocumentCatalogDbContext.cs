namespace EmployeeDocumentsViewer.Features.Documents.Data;

public sealed class DocumentCatalogDbContext(DbContextOptions<DocumentCatalogDbContext> options)
    : DbContext(options)
{
    public DbSet<EmployeeDocumentCatalog> Documents => Set<EmployeeDocumentCatalog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EmployeeDocumentCatalog>(entity =>
        {
            entity.HasNoKey();
            entity.ToView("EmployeeDocumentsCatalog", "HR");
        });
    }
}
