using EmployeeDocumentsViewer.Features.Documents.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmployeeDocumentsViewer.Features.Documents.Data;

public sealed class DocumentCatalogDbContext(DbContextOptions<DocumentCatalogDbContext> options)
    : DbContext(options)
{
    public DbSet<EmployeeDocumentCatalogEntity> EmployeeDocumentCatalog => Set<EmployeeDocumentCatalogEntity>();
    public DbSet<EmployeeDocumentsLookupEntity> EmployeeDocumentsLookup => Set<EmployeeDocumentsLookupEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EmployeeDocumentCatalogEntity>(entity =>
        {
            entity.ToTable("EmployeeDocumentCatalog", "Common");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.CompanyKey).HasMaxLength(10);
            entity.Property(x => x.BlobName).HasMaxLength(512);
            entity.Property(x => x.DocumentTypeDisplay).HasMaxLength(200);
        });

        modelBuilder.Entity<EmployeeDocumentsLookupEntity>(entity =>
        {
            entity.ToView("EmployeeDocumentsLookup", "HR");
            entity.HasKey(x => x.Id);
        });
    }
}
