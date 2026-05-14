using System.Text.Json.Serialization;

namespace EmployeeDocumentsViewer.Features.Documents.List;

public sealed record DocumentReadRow(
    [property: JsonPropertyName("blobName")]
    string BlobName,
    [property: JsonPropertyName("employeeId")]
    int EmployeeId,
    [property: JsonPropertyName("employeeName")]
    string EmployeeName,
    [property: JsonPropertyName("department")]
    string Department,
    [property: JsonPropertyName("documentType")]
    string DocumentType,
    [property: JsonPropertyName("year")]
    int? Year,
    [property: JsonPropertyName("terminationDate")]
    DateTime? TerminationDate,
    [property: JsonPropertyName("updatedUtc")]
    DateTimeOffset? UpdatedUtc);
