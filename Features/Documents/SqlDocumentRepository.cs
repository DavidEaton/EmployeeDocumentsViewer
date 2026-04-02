using Azure;
using Azure.Storage.Blobs;
using EmployeeDocumentsViewer.Configuration;
using EmployeeDocumentsViewer.Features.Documents.List;
using Microsoft.Data.SqlClient;

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
        var orderByClause = BuildOrderByClause(sortColumn, descending);
        var companyKey = company.ToString();

        var sql = $"""
        create table #Base
        (
            BlobName         nvarchar(512)      not null,
            EmployeeId       int                not null,
            Employee         nvarchar(256)      not null,
            Department       nvarchar(256)      null,
            DocumentType     nvarchar(200)      not null,
            [Year]           int                null,
            UpdatedUtc       datetimeoffset(7)  null,
            ContentType      nvarchar(200)      null,
            Active           bit                not null,
            TerminationDate  datetime           null
        );

        insert into #Base
        (
            BlobName,
            EmployeeId,
            Employee,
            Department,
            DocumentType,
            [Year],
            UpdatedUtc,
            ContentType,
            Active,
            TerminationDate
        )
        select
            d.BlobName,
            d.EmployeeId,
            emp.NameLastFirst as Employee,
            emp.HomeDepartment as Department,
            d.DocumentTypeDisplay as DocumentType,
            year(coalesce(d.UpdatedUtc, d.BlobLastModifiedUtc)) as [Year],
            coalesce(d.UpdatedUtc, d.BlobLastModifiedUtc) as UpdatedUtc,
            d.ContentType,
            emp.Active,
            term.TerminationDate
        from Common.EmployeeDocumentCatalog d
        inner join Common.EmployeeEeDocsLookup emp
            on emp.Id = d.EmployeeId
        outer apply
        (
            select top (1)
                tm.TerminationDate
            from HR.Terminations tm
            where tm.PartyID = emp.Id
            order by tm.TerminationDate desc
        ) term
        where
            d.CompanyKey = @CompanyKey
            and d.IsDeleted = 0;

        create table #Filtered
        (
            BlobName         nvarchar(512)      not null,
            EmployeeId       int                not null,
            Employee         nvarchar(256)      not null,
            Department       nvarchar(256)      null,
            DocumentType     nvarchar(200)      not null,
            [Year]           int                null,
            UpdatedUtc       datetimeoffset(7)  null,
            ContentType      nvarchar(200)      null,
            Active           bit                not null,
            TerminationDate  datetime           null
        );

        insert into #Filtered
        (
            BlobName,
            EmployeeId,
            Employee,
            Department,
            DocumentType,
            [Year],
            UpdatedUtc,
            ContentType,
            Active,
            TerminationDate
        )
        select
            BlobName,
            EmployeeId,
            Employee,
            Department,
            DocumentType,
            [Year],
            UpdatedUtc,
            ContentType,
            Active,
            TerminationDate
        from #Base
        where
            @SearchTerm is null
            or @SearchTerm = N''
            or Employee like N'%' + @SearchTerm + N'%'
            or Department like N'%' + @SearchTerm + N'%'
            or DocumentType like N'%' + @SearchTerm + N'%'
            or BlobName like N'%' + @SearchTerm + N'%'
            or cast(EmployeeId as nvarchar(20)) like N'%' + @SearchTerm + N'%'
            or cast([Year] as nvarchar(10)) like N'%' + @SearchTerm + N'%'
            or case when Active = 1 then N'Active' else N'Terminated' end like N'%' + @SearchTerm + N'%'
            or convert(nvarchar(10), TerminationDate, 23) like N'%' + @SearchTerm + N'%';

        select count(*) as TotalCount
        from #Base;

        select count(*) as FilteredCount
        from #Filtered;

        select
            BlobName,
            EmployeeId,
            Employee,
            Department,
            DocumentType,
            [Year],
            UpdatedUtc,
            ContentType,
            Active,
            TerminationDate
        from #Filtered
        order by {orderByClause}
        offset @Start rows fetch next @Length rows only;
        """;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@CompanyKey", companyKey);
        command.Parameters.AddWithValue("@SearchTerm", (object?)searchTerm ?? DBNull.Value);
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
                    : reader.GetDateTimeOffset(updatedUtcOrdinal)));
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

    private static string BuildOrderByClause(DocumentSortColumn sortColumn, bool descending)
    {
        return (sortColumn, descending) switch
        {
            (DocumentSortColumn.EmployeeId, false) => "EmployeeId asc",
            (DocumentSortColumn.EmployeeId, true) => "EmployeeId desc",

            (DocumentSortColumn.Employee, false) => "Employee asc",
            (DocumentSortColumn.Employee, true) => "Employee desc",

            (DocumentSortColumn.Department, false) => "Department asc",
            (DocumentSortColumn.Department, true) => "Department desc",

            (DocumentSortColumn.DocumentType, false) => "DocumentType asc",
            (DocumentSortColumn.DocumentType, true) => "DocumentType desc",

            (DocumentSortColumn.Year, false) => "[Year] asc",
            (DocumentSortColumn.Year, true) => "[Year] desc",

            (DocumentSortColumn.Active, false) => "Active asc",
            (DocumentSortColumn.Active, true) => "Active desc",

            (DocumentSortColumn.TerminationDate, false) => "TerminationDate asc",
            (DocumentSortColumn.TerminationDate, true) => "TerminationDate desc",

            _ => "UpdatedUtc desc, Employee asc"
        };
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