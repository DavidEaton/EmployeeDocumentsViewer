using System.Data;
using Azure.Storage.Blobs;
using EmployeeDocumentsViewer.Configuration;
using Microsoft.Data.SqlClient;

namespace EmployeeDocumentsViewer.Features.Documents.Indexing;

public sealed class SqlDocumentCatalogIndexer(
    ICompanyConnectionStringResolver connectionStringResolver,
    ILogger<SqlDocumentCatalogIndexer> logger) : IDocumentCatalogIndexer
{
    private readonly ICompanyConnectionStringResolver _connectionStringResolver = connectionStringResolver;
    private readonly ILogger<SqlDocumentCatalogIndexer> _logger = logger;

    public async Task SyncCompanyAsync(Company company, CancellationToken cancellationToken)
    {
        var companyKey = company.ToString();
        var sqlConnectionString = _connectionStringResolver.GetSqlConnectionString(company);
        var blobConnectionString = _connectionStringResolver.GetBlobStorageConnectionString(company);

        var rows = new List<DocumentCatalogRow>();
        var indexedUtc = DateTimeOffset.UtcNow;

        var container = CreateDocumentsContainerClient(blobConnectionString);

        await foreach (var blobItem in container.GetBlobsAsync(cancellationToken: cancellationToken))
        {
            if (!DocumentBlobNameParser.TryParseEmployeeDocumentBlobName(
                    blobItem.Name,
                    out var employeeId,
                    out var documentTypeToken))
            {
                continue;
            }

            var documentTypeDisplay = DocumentBlobNameParser.HumanizeDocumentType(documentTypeToken);
            var updatedUtc = DocumentBlobNameParser.TryGetUpdatedDate(blobItem.Metadata, blobItem.Properties.LastModified);
            var contentType = blobItem.Properties.ContentType;
            var eTag = blobItem.Properties.ETag?.ToString();
            var blobNameHash = DocumentBlobNameParser.ComputeBlobNameHash(blobItem.Name);

            rows.Add(new DocumentCatalogRow(
                CompanyKey: companyKey,
                BlobName: blobItem.Name,
                BlobNameHash: blobNameHash,
                EmployeeId: employeeId,
                DocumentTypeToken: documentTypeToken,
                DocumentTypeDisplay: documentTypeDisplay,
                UpdatedUtc: updatedUtc,
                BlobLastModifiedUtc: blobItem.Properties.LastModified,
                ContentType: contentType,
                BlobETag: eTag,
                IsDeleted: false,
                LastIndexedUtc: indexedUtc));
        }

        await using var connection = new SqlConnection(sqlConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            await CreateStageTableAsync(connection, transaction, cancellationToken);
            await BulkCopyStageAsync(connection, transaction, rows, cancellationToken);
            await UpsertStageAsync(connection, transaction, companyKey, cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Indexed {Count} document catalog rows for company {Company}.",
                rows.Count,
                company);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static BlobContainerClient CreateDocumentsContainerClient(string blobConnectionString)
    {
        const string containerName = "hrdocs";
        return new BlobContainerClient(blobConnectionString, containerName);
    }

    private static async Task CreateStageTableAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        const string sql = """
            create table #CatalogStage
            (
                CompanyKey            varchar(10)       not null,
                BlobName              nvarchar(512)     not null,
                BlobNameHash          varbinary(32)     not null,
                EmployeeId            int               not null,
                DocumentTypeToken     nvarchar(200)     not null,
                DocumentTypeDisplay   nvarchar(200)     not null,
                UpdatedUtc            datetimeoffset(7) null,
                BlobLastModifiedUtc   datetimeoffset(7) null,
                ContentType           nvarchar(200)     null,
                BlobETag              nvarchar(128)     null,
                LastIndexedUtc        datetimeoffset(7) not null
            );
            """;

        await using var command = new SqlCommand(sql, connection, transaction);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task BulkCopyStageAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        IReadOnlyCollection<DocumentCatalogRow> rows,
        CancellationToken cancellationToken)
    {
        var table = new DataTable();
        table.Columns.Add("CompanyKey", typeof(string));
        table.Columns.Add("BlobName", typeof(string));
        table.Columns.Add("BlobNameHash", typeof(byte[]));
        table.Columns.Add("EmployeeId", typeof(int));
        table.Columns.Add("DocumentTypeToken", typeof(string));
        table.Columns.Add("DocumentTypeDisplay", typeof(string));
        table.Columns.Add("UpdatedUtc", typeof(DateTimeOffset));
        table.Columns.Add("BlobLastModifiedUtc", typeof(DateTimeOffset));
        table.Columns.Add("ContentType", typeof(string));
        table.Columns.Add("BlobETag", typeof(string));
        table.Columns.Add("LastIndexedUtc", typeof(DateTimeOffset));

        foreach (var row in rows)
        {
            table.Rows.Add(
                row.CompanyKey,
                row.BlobName,
                row.BlobNameHash,
                row.EmployeeId,
                row.DocumentTypeToken,
                row.DocumentTypeDisplay,
                row.UpdatedUtc.HasValue ? row.UpdatedUtc.Value : DBNull.Value,
                row.BlobLastModifiedUtc.HasValue ? row.BlobLastModifiedUtc.Value : DBNull.Value,
                row.ContentType is not null ? row.ContentType : DBNull.Value,
                row.BlobETag is not null ? row.BlobETag : DBNull.Value,
                row.LastIndexedUtc);
        }

        using var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default, transaction)
        {
            DestinationTableName = "#CatalogStage",
            BatchSize = 5000
        };

        bulkCopy.ColumnMappings.Add("CompanyKey", "CompanyKey");
        bulkCopy.ColumnMappings.Add("BlobName", "BlobName");
        bulkCopy.ColumnMappings.Add("BlobNameHash", "BlobNameHash");
        bulkCopy.ColumnMappings.Add("EmployeeId", "EmployeeId");
        bulkCopy.ColumnMappings.Add("DocumentTypeToken", "DocumentTypeToken");
        bulkCopy.ColumnMappings.Add("DocumentTypeDisplay", "DocumentTypeDisplay");
        bulkCopy.ColumnMappings.Add("UpdatedUtc", "UpdatedUtc");
        bulkCopy.ColumnMappings.Add("BlobLastModifiedUtc", "BlobLastModifiedUtc");
        bulkCopy.ColumnMappings.Add("ContentType", "ContentType");
        bulkCopy.ColumnMappings.Add("BlobETag", "BlobETag");
        bulkCopy.ColumnMappings.Add("LastIndexedUtc", "LastIndexedUtc");

        await bulkCopy.WriteToServerAsync(table, cancellationToken);
    }

    private static async Task UpsertStageAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string companyKey,
        CancellationToken cancellationToken)
    {
        const string sql = """
            update target
            set
                target.EmployeeId = src.EmployeeId,
                target.DocumentTypeToken = src.DocumentTypeToken,
                target.DocumentTypeDisplay = src.DocumentTypeDisplay,
                target.UpdatedUtc = src.UpdatedUtc,
                target.BlobLastModifiedUtc = src.BlobLastModifiedUtc,
                target.ContentType = src.ContentType,
                target.BlobETag = src.BlobETag,
                target.IsDeleted = 0,
                target.LastIndexedUtc = src.LastIndexedUtc
            from Common.EmployeeDocumentCatalog target
            inner join #CatalogStage src
                on src.CompanyKey = target.CompanyKey
               and src.BlobNameHash = target.BlobNameHash
               and src.BlobName = target.BlobName;

            insert into Common.EmployeeDocumentCatalog
            (
                CompanyKey,
                BlobName,
                BlobNameHash,
                EmployeeId,
                DocumentTypeToken,
                DocumentTypeDisplay,
                UpdatedUtc,
                BlobLastModifiedUtc,
                ContentType,
                BlobETag,
                IsDeleted,
                LastIndexedUtc
            )
            select
                src.CompanyKey,
                src.BlobName,
                src.BlobNameHash,
                src.EmployeeId,
                src.DocumentTypeToken,
                src.DocumentTypeDisplay,
                src.UpdatedUtc,
                src.BlobLastModifiedUtc,
                src.ContentType,
                src.BlobETag,
                0,
                src.LastIndexedUtc
            from #CatalogStage src
            where not exists
            (
                select 1
                from Common.EmployeeDocumentCatalog target
                where target.CompanyKey = src.CompanyKey
                  and target.BlobNameHash = src.BlobNameHash
                  and target.BlobName = src.BlobName
            );

            update target
            set
                target.IsDeleted = 1,
                target.LastIndexedUtc = sysutcdatetime()
            from Common.EmployeeDocumentCatalog target
            where
                target.CompanyKey = @CompanyKey
                and target.IsDeleted = 0
                and not exists
                (
                    select 1
                    from #CatalogStage src
                    where src.CompanyKey = target.CompanyKey
                      and src.BlobNameHash = target.BlobNameHash
                      and src.BlobName = target.BlobName
                );
            """;

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@CompanyKey", companyKey);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}