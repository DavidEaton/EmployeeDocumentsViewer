using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Azure.Storage.Blobs.Models;

namespace EmployeeDocumentsViewer.Features.Documents;

public static partial class DocumentBlobNameParser
{
    public static bool TryParseEmployeeDocumentBlobName(
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

    public static byte[] ComputeBlobNameHash(string blobName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(blobName);

        return SHA256.HashData(Encoding.UTF8.GetBytes(blobName));
    }
    
    public static DateTimeOffset? TryGetUpdatedDate(
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

    public static DateTimeOffset? TryGetUpdatedDate(BlobItemProperties properties, IDictionary<string, string> metadata)
        => TryGetUpdatedDate(metadata, properties.LastModified);

    public static string HumanizeDocumentType(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return token;

        var withSpaces = PascalCaseBoundaryRegex().Replace(token, " $1");
        return withSpaces.Replace("  ", " ").Trim();
    }

    [GeneratedRegex(@"\(\d+\)$", RegexOptions.Compiled)]
    private static partial Regex TrailingInstanceSuffixRegex();

    [GeneratedRegex("([A-Z])", RegexOptions.Compiled)]
    private static partial Regex PascalCaseBoundaryRegex();
}