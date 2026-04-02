using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using Azure;
using Azure.Storage.Blobs;
using EmployeeDocumentsViewer.Configuration;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace EmployeeDocumentsViewer.Features.Documents;

public sealed partial class SqlDocumentRepository(
    ICompanyConnectionStringResolver connectionResolver,
    IOptions<StorageOptions> storageOptions,
    ILogger<SqlDocumentRepository> logger)
    : IDocumentRepository
{
    private readonly string _documentsContainerName = storageOptions.Value.DocumentsContainerName;
    private readonly ILogger<SqlDocumentRepository> _logger = logger;

    public async Task<(int TotalCount, int FilteredCount, IReadOnlyList<DocumentRecord> Items)> SearchAsync(
        Company company,
        string? searchTerm,
        string? sortColumn,
        string? sortDirection,
        int start,
        int length,
        CancellationToken cancellationToken)
    {
        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["Company"] = company.ToString(),
            ["SearchTerm"] = searchTerm,
            ["SortColumn"] = sortColumn,
            ["SortDirection"] = sortDirection,
            ["Start"] = start,
            ["Length"] = length
        });

        _logger.LogInformation(
            "Searching documents for company {Company}. Start={Start}, Length={Length}, Sort={SortColumn} {SortDirection}.",
            company, start, length, sortColumn, sortDirection);

        var employees = await LoadEmployeesAsync(company, cancellationToken);
        _logger.LogDebug("Loaded {EmployeeCount} employees from SQL for company {Company}.", employees.Count, company);

        if (employees.Count == 0)
            return (0, 0, []);

        var blobs = await LoadDocumentBlobsAsync(company, cancellationToken);

        _logger.LogDebug("Loaded {BlobCount} candidate blobs from container {ContainerName} for company {Company}.",
            blobs.Count, _documentsContainerName, company);

        var joined = blobs
            .Select(blob => TryCreateRecord(blob, employees))
            .Where(record => record is not null)
            .Cast<DocumentRecord>()
            .ToList();

        var totalCount = joined.Count;

        IEnumerable<DocumentRecord> filteredQuery = joined;

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();

            filteredQuery = filteredQuery.Where(record =>
            {
                var employeeIdText = record.EmployeeId.ToString(CultureInfo.InvariantCulture);
                var yearText = record.Year.ToString(CultureInfo.InvariantCulture);
                var statusText = record.Active ? "active" : "terminated";
                var terminationDateText = record.TerminationDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

                return
                    record.Employee.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || record.Department.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || record.DocumentType.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || record.BlobName.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || statusText.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || employeeIdText.Contains(term)
                    || yearText.Contains(term)
                    || (terminationDateText is not null && terminationDateText.Contains(term));
            });
        }

        var filtered = ApplySort(filteredQuery, sortColumn, sortDirection).ToList();
        var filteredCount = filtered.Count;

        var page = filtered
            .Skip(Math.Max(0, start))
            .Take(length <= 0 ? 10 : length)
            .ToArray();

        _logger.LogInformation(
            "Document search complete for company {Company}. Total={TotalCount}, Filtered={FilteredCount}, Returned={ReturnedCount}.",
            company, totalCount, filteredCount, page.Length);

        return (totalCount, filteredCount, page);
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

        Response<bool> exists;
        try
        {
            exists = await blobClient.ExistsAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex,
                "Blob existence check failed for company {Company}, blob {BlobName}.",
                company, blobName);
            return null;
        }

        if (!exists.Value)
        {
            _logger.LogWarning(
                "Blob not found for company {Company}, blob {BlobName}.",
                company, blobName);
            return null;
        }

        _logger.LogDebug(
            "Opening blob {BlobName} for company {Company}.",
            blobName, company);

        var download = await blobClient
            .DownloadStreamingAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

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

    private async Task<Dictionary<int, EmployeeLookup>> LoadEmployeesAsync(
    Company company,
    CancellationToken cancellationToken)
    {
        const string sql = """
        select
            emp.Id,
            emp.NameLastFirst,
            emp.HomeDepartment,
            emp.Active,
            term.TerminationDate
        from Common.EmployeeEeDocsLookup emp
        outer apply
        (
            select top (1)
                tm.TerminationDate
            from HR.Terminations tm
            where tm.PartyID = emp.Id
            order by tm.TerminationDate desc
        ) term
        order by emp.Id;
        """;

        try
        {
            var result = new Dictionary<int, EmployeeLookup>();

            await using var connection = new SqlConnection(connectionResolver.GetSqlConnectionString(company));
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            await using var command = new SqlCommand(sql, connection)
            {
                CommandType = CommandType.Text
            };

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var id = reader.GetInt32(reader.GetOrdinal("Id"));

                var employee = new EmployeeLookup(
                    Id: id,
                    Name: reader["NameLastFirst"] as string ?? string.Empty,
                    Department: reader["HomeDepartment"] as string ?? string.Empty,
                    Active: reader["Active"] is bool active && active,
                    TerminationDate: reader["TerminationDate"] is DBNull
                        ? null
                        : (DateTime?)reader["TerminationDate"]);

                result[id] = employee;
            }

            _logger.LogInformation(
                "Loaded {EmployeeCount} employees from SQL for company {Company}.",
                result.Count, company);

            return result;
        }
        catch (SqlException ex)
        {
            _logger.LogError(ex,
                "SQL load failed while loading employees for company {Company}.",
                company);
            throw;
        }
    }

    private async Task<IReadOnlyList<BlobLookup>> LoadDocumentBlobsAsync(
    Company company,
    CancellationToken cancellationToken)
    {
        var container = CreateDocumentsContainerClient(company);
        var items = new List<BlobLookup>();
        var skippedBlobCount = 0;

        try
        {
            await foreach (var blobItem in container.GetBlobsAsync(cancellationToken: cancellationToken))
            {
                if (!TryParseEmployeeDocumentBlobName(blobItem.Name, out var employeeId, out var documentTypeToken))
                {
                    skippedBlobCount++;
                    _logger.LogDebug(
                        "Skipping blob with unexpected name format in company {Company}: {BlobName}",
                        company, blobItem.Name);
                    continue;
                }

                var updatedUtc = TryGetUpdatedDate(blobItem.Metadata, blobItem.Properties.LastModified);

                items.Add(new BlobLookup(
                    BlobName: blobItem.Name,
                    EmployeeId: employeeId,
                    DocumentTypeToken: documentTypeToken,
                    UpdatedUtc: updatedUtc,
                    ContentType: blobItem.Properties.ContentType));
            }

            _logger.LogInformation(
                "Enumerated {BlobCount} document blobs for company {Company}. Skipped={SkippedBlobCount}.",
                items.Count, company, skippedBlobCount);

            return items;
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex,
                "Blob enumeration failed for company {Company}, container {ContainerName}.",
                company, _documentsContainerName);
            throw;
        }
    }

    private DocumentRecord? TryCreateRecord(
        BlobLookup blob,
        IReadOnlyDictionary<int, EmployeeLookup> employees)
    {
        if (!employees.TryGetValue(blob.EmployeeId, out var employee))
            return null;

        return new DocumentRecord(
            BlobName: blob.BlobName,
            EmployeeId: blob.EmployeeId,
            Employee: employee.Name,
            Department: employee.Department,
            DocumentType: HumanizeDocumentType(blob.DocumentTypeToken),
            Year: (blob.UpdatedUtc ?? DateTimeOffset.UtcNow).Year,
            UpdatedUtc: blob.UpdatedUtc,
            Active: employee.Active,
            TerminationDate: employee.TerminationDate,
            ContentType: blob.ContentType);
    }

private BlobContainerClient CreateDocumentsContainerClient(Company company)
{
    var connectionString = connectionResolver.GetBlobStorageConnectionString(company);

    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException(
            $"Blob connection string is empty for {company}");
    }

    _logger.LogInformation(
        "Blob connection string present for {Company}. Length={Length}",
        company,
        connectionString.Length);

    return new BlobContainerClient(connectionString, _documentsContainerName);
}

    private static IEnumerable<DocumentRecord> ApplySort(
        IEnumerable<DocumentRecord> source,
        string? sortColumn,
        string? sortDirection)
    {
        var descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);

        return (sortColumn ?? string.Empty).ToLowerInvariant() switch
        {
            "employeeid" => descending
                ? source.OrderByDescending(x => x.EmployeeId).ThenByDescending(x => x.BlobName)
                : source.OrderBy(x => x.EmployeeId).ThenBy(x => x.BlobName),

            "employee" => descending
                ? source.OrderByDescending(x => x.Employee).ThenByDescending(x => x.BlobName)
                : source.OrderBy(x => x.Employee).ThenBy(x => x.BlobName),

            "department" => descending
                ? source.OrderByDescending(x => x.Department).ThenByDescending(x => x.Employee)
                : source.OrderBy(x => x.Department).ThenBy(x => x.Employee),

            "documenttype" => descending
                ? source.OrderByDescending(x => x.DocumentType).ThenByDescending(x => x.Employee)
                : source.OrderBy(x => x.DocumentType).ThenBy(x => x.Employee),

            "year" => descending
                ? source.OrderByDescending(x => x.Year).ThenByDescending(x => x.Employee)
                : source.OrderBy(x => x.Year).ThenBy(x => x.Employee),

            "active" => descending
                ? source.OrderByDescending(x => x.Active).ThenByDescending(x => x.Employee)
                : source.OrderBy(x => x.Active).ThenBy(x => x.Employee),

            "terminationdate" => descending
                ? source.OrderByDescending(x => x.TerminationDate).ThenByDescending(x => x.Employee)
                : source.OrderBy(x => x.TerminationDate).ThenBy(x => x.Employee),

            _ => descending
                ? source.OrderByDescending(x => x.UpdatedUtc).ThenByDescending(x => x.Employee)
                : source.OrderByDescending(x => x.UpdatedUtc).ThenBy(x => x.Employee)
        };
    }

    private static bool TryParseEmployeeDocumentBlobName(
        string blobName,
        out int employeeId,
        out string documentTypeToken)
    {
        employeeId = 0;
        documentTypeToken = string.Empty;

        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(blobName);
        if (string.IsNullOrWhiteSpace(fileNameWithoutExtension))
            return false;

        var underscoreIndex = fileNameWithoutExtension.IndexOf('_');
        if (underscoreIndex <= 0)
            return false;

        var employeeToken = fileNameWithoutExtension[..underscoreIndex];
        if (!int.TryParse(employeeToken, NumberStyles.None, CultureInfo.InvariantCulture, out employeeId))
            return false;

        var remainder = fileNameWithoutExtension[(underscoreIndex + 1)..];
        if (string.IsNullOrWhiteSpace(remainder))
            return false;

        documentTypeToken = TrailingInstanceSuffixRegex().Replace(remainder, string.Empty);
        return !string.IsNullOrWhiteSpace(documentTypeToken);
    }

    private static DateTimeOffset? TryGetUpdatedDate(
        IDictionary<string, string> metadata,
        DateTimeOffset? lastModified)
    {
        if (metadata.TryGetValue("UpdatedDate", out var updatedDate)
            && DateTimeOffset.TryParse(
                updatedDate,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal | DateTimeStyles.AllowWhiteSpaces,
                out var parsed))
        {
            return parsed;
        }

        return lastModified;
    }

    private static string HumanizeDocumentType(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return token;

        var withSpaces = PascalCaseBoundaryRegex().Replace(token, " $1");
        return withSpaces.Replace("  ", " ").Trim();
    }

    private static string GetContentTypeFromFileName(string blobName)
    {
        var extension = Path.GetExtension(blobName).ToLowerInvariant();

        return extension switch
        {
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".txt" => "text/plain",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            _ => "application/octet-stream"
        };
    }

    private sealed record EmployeeLookup(
        int Id,
        string Name,
        string Department,
        bool Active,
        DateTime? TerminationDate);

    private sealed record BlobLookup(
        string BlobName,
        int EmployeeId,
        string DocumentTypeToken,
        DateTimeOffset? UpdatedUtc,
        string? ContentType);

    [GeneratedRegex(@"\(\d+\)$", RegexOptions.Compiled)]
    private static partial Regex TrailingInstanceSuffixRegex();

    [GeneratedRegex("([A-Z])", RegexOptions.Compiled)]
    private static partial Regex PascalCaseBoundaryRegex();
}