using Azure;
using Azure.Storage.Blobs;
using EmployeeDocumentsViewer.Configuration;
using EmployeeDocumentsViewer.Features.Documents.Data;
using EmployeeDocumentsViewer.Features.Documents.List;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EmployeeDocumentsViewer.Features.Documents;

public sealed class SqlDocumentRepository(
    ICompanyConnectionStringResolver connectionStringResolver,
    IOptions<StorageOptions> storageOptions,
    ILogger<SqlDocumentRepository> logger) : IDocumentRepository
{
    private readonly ICompanyConnectionStringResolver _connectionStringResolver = connectionStringResolver;
    private readonly StorageOptions _storageOptions = storageOptions.Value;
    private readonly ILogger<SqlDocumentRepository> _logger = logger;

    public async Task<(int TotalCount, int FilteredCount, IReadOnlyList<DocumentReadRow> Items)> SearchAsync(
        Company company,
        int page,
        int size,
        IReadOnlyList<FilterDescriptor> filters,
        IReadOnlyList<SortDescriptor> sorters,
        CancellationToken cancellationToken)
    {
        var options = new DbContextOptionsBuilder<DocumentCatalogDbContext>()
            .UseSqlServer(_connectionStringResolver.GetSqlConnectionString(company))
            .Options;

        await using var context = new DocumentCatalogDbContext(options);

        var baseQuery = context.Documents
            .AsNoTracking();

        var totalCount = await baseQuery.CountAsync(cancellationToken);
        var filtered = ApplyFilters(baseQuery, filters);
        var filteredCount = await filtered.CountAsync(cancellationToken);
        var sorted = ApplySorting(filtered, sorters);

        var rows = await sorted
            .Skip((page - 1) * size)
            .Take(size)
            .Select(x => new DocumentReadRow(
                BlobName: string.Empty,
                EmployeeId: x.EmployeeId,
                EmployeeName: x.EmployeeName,
                Department: x.HomeDepartment,
                DocumentType: x.DocumentTypeDisplay,
                Year: x.Year,
                TerminationDate: x.TerminationDate,
                UpdatedUtc: null))
            .ToListAsync(cancellationToken);

        return (totalCount, filteredCount, rows);
    }

    private static IQueryable<EmployeeDocumentCatalog> ApplyFilters(IQueryable<EmployeeDocumentCatalog> query, IReadOnlyList<FilterDescriptor> filters)
    {
        foreach (var filter in filters.Where(f => !string.IsNullOrWhiteSpace(f.Value)))
        {
            var value = filter.Value!.Trim();
            var term = $"%{value}%";
            switch (filter.Field.ToLowerInvariant())
            {
                case "employeeid" when int.TryParse(value, out var employeeId):
                    query = query.Where(x => x.EmployeeId == employeeId);
                    break;
                case "employeename":
                    query = query.Where(x => EF.Functions.Like(x.EmployeeName, term));
                    break;
                case "department":
                case "homedepartment":
                    query = query.Where(x => EF.Functions.Like(x.HomeDepartment, term));
                    break;
                case "documenttype":
                    query = query.Where(x => EF.Functions.Like(x.DocumentTypeDisplay, term));
                    break;
                case "year" when int.TryParse(value, out var year):
                    query = query.Where(x => x.Year == year);
                    break;
            }
        }

        return query;
    }

    private static IQueryable<EmployeeDocumentCatalog> ApplySorting(IQueryable<EmployeeDocumentCatalog> query, IReadOnlyList<SortDescriptor> sorters)
    {
        var sorter = sorters.FirstOrDefault();
        var desc = string.Equals(sorter?.Dir, "desc", StringComparison.OrdinalIgnoreCase);

        return (sorter?.Field?.ToLowerInvariant(), desc) switch
        {
            ("employeeid", false) => query.OrderBy(x => x.EmployeeId),
            ("employeeid", true) => query.OrderByDescending(x => x.EmployeeId),
            ("employeename", false) => query.OrderBy(x => x.EmployeeName),
            ("employeename", true) => query.OrderByDescending(x => x.EmployeeName),
            ("department", false) => query.OrderBy(x => x.HomeDepartment),
            ("department", true) => query.OrderByDescending(x => x.HomeDepartment),
            ("documenttype", false) => query.OrderBy(x => x.DocumentTypeDisplay),
            ("documenttype", true) => query.OrderByDescending(x => x.DocumentTypeDisplay),
            ("year", false) => query.OrderBy(x => x.Year),
            ("year", true) => query.OrderByDescending(x => x.Year),
            _ => query.OrderBy(x => x.EmployeeName)
        };
    }

    public async Task<BlobDocumentStream?> OpenReadAsync(Company company, string blobName, CancellationToken cancellationToken)
    {
        var container = CreateDocumentsContainerClient(company);
        var blobClient = container.GetBlobClient(blobName);

        try
        {
            var download = await blobClient.DownloadStreamingAsync(cancellationToken: cancellationToken);
            return new BlobDocumentStream
            {
                BlobName = blobName,
                Content = download.Value.Content,
                Length = download.Value.Details.ContentLength,
                ContentType = download.Value.Details.ContentType ?? "application/octet-stream"
            };
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogWarning("Blob not found for company {Company}, blob {BlobName}.", company, blobName);
            return null;
        }
    }

    private BlobContainerClient CreateDocumentsContainerClient(Company company)
    {
        var connectionString = _connectionStringResolver.GetBlobStorageConnectionString(company);
        return new BlobContainerClient(connectionString, _storageOptions.DocumentsContainerName);
    }
}
