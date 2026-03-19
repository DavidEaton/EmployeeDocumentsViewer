namespace EmployeeDocumentsViewer.Features.Documents;

public sealed class BlobDocumentStream : IAsyncDisposable
{
    public required Stream Content { get; init; }
    public required long? Length { get; init; }
    public required string ContentType { get; init; }
    public required string BlobName { get; init; }

    public ValueTask DisposeAsync() => Content.DisposeAsync();
}