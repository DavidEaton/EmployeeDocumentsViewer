using Microsoft.EntityFrameworkCore;

namespace EmployeeDocumentsViewer.Features.Documents.Data;

public sealed class DocumentCatalogDbContext(DbContextOptions<DocumentCatalogDbContext> options)
    : DbContext(options)
{
    public DbSet<EmployeeDocumentCatalog> EmployeeDocumentCatalog => Set<EmployeeDocumentCatalog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EmployeeDocumentCatalog>(entity =>
        {
            entity.ToTable("EmployeeDocumentCatalog", "HR");
            entity.HasKey(x => x.Id);
        });
    }
}
