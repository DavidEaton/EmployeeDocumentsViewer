using Azure;
using Azure.Storage.Blobs;
using EmployeeDocumentsViewer.Configuration;
using EmployeeDocumentsViewer.Features.Documents.List;
using Microsoft.Data.SqlClient;
using System.Data;

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
        var connectionString = _connectionStringResolver.GetSqlConnectionString(company);

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(
            "Common.usp_EmployeeDocumentCatalog_Search",
            connection)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 60
        };

        command.Parameters.AddWithValue("@SearchTerm", (object?)searchTerm ?? DBNull.Value);
        command.Parameters.AddWithValue("@SortColumn", sortColumn.ToString());
        command.Parameters.AddWithValue("@SortDescending", descending);
        command.Parameters.AddWithValue("@Start", start);
        command.Parameters.AddWithValue("@Length", length);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        await reader.ReadAsync(cancellationToken);
        var totalCount = reader.GetInt32(0);

        await reader.NextResultAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        var filteredCount = reader.GetInt32(0);

        await reader.NextResultAsync(cancellationToken);

        var blobNameOrdinal = reader.GetOrdinal("BlobName");
        var employeeIdOrdinal = reader.GetOrdinal("EmployeeId");
        var employeeOrdinal = reader.GetOrdinal("Employee");
        var departmentOrdinal = reader.GetOrdinal("Department");
        var documentTypeOrdinal = reader.GetOrdinal("DocumentType");
        var yearOrdinal = reader.GetOrdinal("Year");
        var activeOrdinal = reader.GetOrdinal("Active");
        var terminationDateOrdinal = reader.GetOrdinal("TerminationDate");
        var updatedUtcOrdinal = reader.GetOrdinal("UpdatedUtc");

        var rows = new List<DocumentReadRow>();

        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new DocumentReadRow(
                BlobName: reader.GetString(blobNameOrdinal),
                EmployeeId: reader.GetInt32(employeeIdOrdinal),
                Employee: reader.GetString(employeeOrdinal),
                Department: reader.IsDBNull(departmentOrdinal)
                    ? string.Empty
                    : reader.GetString(departmentOrdinal),
                DocumentType: reader.GetString(documentTypeOrdinal),
                Year: reader.IsDBNull(yearOrdinal) ? null : reader.GetInt32(yearOrdinal),
                Active: reader.GetBoolean(activeOrdinal),
                TerminationDate: reader.IsDBNull(terminationDateOrdinal)
                    ? null
                    : reader.GetDateTime(terminationDateOrdinal),
                UpdatedUtc: reader.IsDBNull(updatedUtcOrdinal)
                    ? null
                    : reader.GetDateTimeOffset(updatedUtcOrdinal),
                CompanyKey: company.ToString()));
        }

        return (totalCount, filteredCount, rows);
    }

    public async Task<BlobDocumentStream?> OpenReadAsync(
        Company company,
        string blobName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(blobName))
            return null;

        var container = CreateDocumentsContainerClient(company);
        var blobClient = container.GetBlobClient(blobName);

        try
        {
            var download = await blobClient
                .DownloadStreamingAsync(cancellationToken: cancellationToken);

            var contentType = !string.IsNullOrWhiteSpace(download.Value.Details.ContentType)
                ? download.Value.Details.ContentType
                : GetContentTypeFromFileName(blobName);

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
}