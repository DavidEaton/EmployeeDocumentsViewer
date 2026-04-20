using Azure;
using Azure.Storage.Blobs;
using System.Diagnostics;
using EmployeeDocumentsViewer.Configuration;
using EmployeeDocumentsViewer.Features.Documents.Data;
using EmployeeDocumentsViewer.Features.Documents.List;
using Microsoft.EntityFrameworkCore;

namespace EmployeeDocumentsViewer.Features.Documents;

public sealed class SqlDocumentRepository(
    ICompanyConnectionStringResolver connectionStringResolver,
    ILogger<SqlDocumentRepository> logger) : IDocumentRepository
{
    private readonly ICompanyConnectionStringResolver _connectionStringResolver = connectionStringResolver;
    private readonly ILogger<SqlDocumentRepository> _logger = logger;

    public async Task<(int TotalCount, int FilteredCount, IReadOnlyList<DocumentReadRow> Items)> SearchAsync(
        Company company,
        string? searchTerm,
        DocumentSortColumn sortColumn,
        bool descending,
        int start,
        int length,
        CancellationToken cancellationToken)
    {
        using var activity = Telemetry.ActivitySource.StartActivity("documents.search", ActivityKind.Internal);

        var companyKey = company.ToString();
        var hasSearchTerm = !string.IsNullOrWhiteSpace(searchTerm);

        activity?.SetTag("company.key", companyKey);
        activity?.SetTag("search.term.present", hasSearchTerm);
        activity?.SetTag("sort.column", sortColumn.ToString());
        activity?.SetTag("sort.descending", descending);
        activity?.SetTag("page.start", Math.Max(0, start));
        activity?.SetTag("page.length", Math.Clamp(length, 1, 500));

        Telemetry.SearchRequests.Add(1,
            new KeyValuePair<string, object?>("company.key", companyKey));

        var connectionString = _connectionStringResolver.GetSqlConnectionString(company);

        var options = new DbContextOptionsBuilder<DocumentCatalogDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        await using var context = new DocumentCatalogDbContext(options);

        var query = context.EmployeeDocumentCatalog
            .AsNoTracking()
            .Where(catalog => !catalog.IsDeleted && catalog.CompanyKey == companyKey)
            .GroupJoin(
                context.EmployeeDocumentsLookup.AsNoTracking(),
                catalog => catalog.EmployeeId,
                employee => employee.Id,
                (catalog, employeeGroup) => new
                {
                    catalog,
                    employeeGroup
                })
            .SelectMany(
                joined => joined.employeeGroup.DefaultIfEmpty(),
                (joined, employee) => new DocumentQueryRow
                {
                    BlobName = joined.catalog.BlobName,
                    EmployeeId = joined.catalog.EmployeeId,
                    Employee = employee != null ? employee.NameLastFirst : null,
                    Department = employee != null ? employee.HomeDepartment : null,
                    DocumentType = joined.catalog.DocumentTypeDisplay,
                    UpdatedUtc = joined.catalog.UpdatedUtc ?? joined.catalog.BlobLastModifiedUtc,
                    Active = employee != null && employee.Active
                });

        var totalCount = await query.CountAsync(cancellationToken);

        var normalizedSearchTerm = searchTerm?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedSearchTerm))
        {
            var term = $"%{normalizedSearchTerm}%";
            var employeeIdSearch = int.TryParse(normalizedSearchTerm, out var parsedEmployeeId)
                ? parsedEmployeeId
                : (int?)null;

            query = query.Where(row =>
                EF.Functions.Like(row.BlobName, term)
                || EF.Functions.Like(row.DocumentType, term)
                || (row.Employee != null && EF.Functions.Like(row.Employee, term))
                || (row.Department != null && EF.Functions.Like(row.Department, term))
                || (employeeIdSearch.HasValue && row.EmployeeId == employeeIdSearch.Value));
        }

        var filteredCount = await query.CountAsync(cancellationToken);

        query = ApplySorting(query, sortColumn, descending);

        var pageSize = Math.Clamp(length, 1, 500);

        var page = await query
            .Skip(Math.Max(0, start))
            .Take(pageSize)
            .Select(row => new DocumentReadRow(
                BlobName: row.BlobName,
                EmployeeId: row.EmployeeId,
                Employee: row.Employee ?? row.EmployeeId.ToString(),
                Department: row.Department ?? string.Empty,
                DocumentType: row.DocumentType,
                Year: row.UpdatedUtc.HasValue ? row.UpdatedUtc.Value.Year : null,
                Active: row.Active,
                TerminationDate: null,
                UpdatedUtc: row.UpdatedUtc,
                CompanyKey: companyKey))
            .ToListAsync(cancellationToken);

        activity?.SetTag("search.total_count", totalCount);
        activity?.SetTag("search.filtered_count", filteredCount);
        activity?.SetTag("search.result_count", page.Count);

        return (totalCount, filteredCount, page);
    }

    private static IQueryable<DocumentQueryRow> ApplySorting(
        IQueryable<DocumentQueryRow> query,
        DocumentSortColumn sortColumn,
        bool descending)
    {
        return (sortColumn, descending) switch
        {
            (DocumentSortColumn.EmployeeId, false) => query.OrderBy(x => x.EmployeeId),
            (DocumentSortColumn.EmployeeId, true) => query.OrderByDescending(x => x.EmployeeId),
            (DocumentSortColumn.Employee, false) => query.OrderBy(x => x.Employee),
            (DocumentSortColumn.Employee, true) => query.OrderByDescending(x => x.Employee),
            (DocumentSortColumn.Department, false) => query.OrderBy(x => x.Department),
            (DocumentSortColumn.Department, true) => query.OrderByDescending(x => x.Department),
            (DocumentSortColumn.DocumentType, false) => query.OrderBy(x => x.DocumentType),
            (DocumentSortColumn.DocumentType, true) => query.OrderByDescending(x => x.DocumentType),
            (DocumentSortColumn.Active, false) => query.OrderBy(x => x.Active),
            (DocumentSortColumn.Active, true) => query.OrderByDescending(x => x.Active),
            (DocumentSortColumn.Year, false) => query.OrderBy(x => x.UpdatedUtc),
            (DocumentSortColumn.Year, true) => query.OrderByDescending(x => x.UpdatedUtc),
            _ when descending => query.OrderByDescending(x => x.UpdatedUtc),
            _ => query.OrderBy(x => x.UpdatedUtc)
        };
    }

    public async Task<BlobDocumentStream?> OpenReadAsync(
        Company company,
        string blobName,
        CancellationToken cancellationToken)
    {
        using var activity = Telemetry.ActivitySource.StartActivity("documents.open", ActivityKind.Internal);

        var companyKey = company.ToString();
        var extension = Path.GetExtension(blobName).ToLowerInvariant();

        activity?.SetTag("company.key", companyKey);
        activity?.SetTag("blob.extension", extension);
        activity?.SetTag("blob.name.present", !string.IsNullOrWhiteSpace(blobName));

        Telemetry.OpenRequests.Add(1,
            new KeyValuePair<string, object?>("company.key", companyKey));

        if (string.IsNullOrWhiteSpace(blobName))
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Blob name was empty.");
            return null;
        }

        var container = CreateDocumentsContainerClient(company);
        var blobClient = container.GetBlobClient(blobName);

        try
        {
            var download = await blobClient
                .DownloadStreamingAsync(cancellationToken: cancellationToken);

            var contentType = !string.IsNullOrWhiteSpace(download.Value.Details.ContentType)
                ? download.Value.Details.ContentType
                : GetContentTypeFromFileName(blobName);

            activity?.SetTag("blob.length", download.Value.Details.ContentLength);
            activity?.SetTag("blob.content_type", contentType);

            return new BlobDocumentStream
            {
                BlobName = blobName,
                Content = download.Value.Content,
                Length = download.Value.Details.ContentLength,
                ContentType = contentType
            };
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            Telemetry.OpenNotFound.Add(1,
                new KeyValuePair<string, object?>("company.key", companyKey));

            activity?.SetStatus(ActivityStatusCode.Error, "Blob not found.");

            _logger.LogWarning(
                "Blob not found for company {Company}, blob {BlobName}.",
                company,
                blobName);

            return null;
        }
    }

    private BlobContainerClient CreateDocumentsContainerClient(Company company)
    {
        var connectionString = _connectionStringResolver.GetBlobStorageConnectionString(company);
        return new BlobContainerClient(connectionString, "hrdocs");
    }

    private static string GetContentTypeFromFileName(string blobName)
    {
        var extension = Path.GetExtension(blobName);

        return extension.ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".tif" => "image/tiff",
            ".tiff" => "image/tiff",
            _ => "application/octet-stream"
        };
    }

    private sealed class DocumentQueryRow
    {
        public string BlobName { get; init; } = string.Empty;
        public int EmployeeId { get; init; }
        public string? Employee { get; init; }
        public string? Department { get; init; }
        public string DocumentType { get; init; } = string.Empty;
        public DateTimeOffset? UpdatedUtc { get; init; }
        public bool Active { get; init; }
    }
}
