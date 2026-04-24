using FastEndpoints;

namespace EmployeeDocumentsViewer.Features.Documents.Open;

public sealed class Endpoint(IDocumentRepository repository)
    : Endpoint<Request>
{
    public override void Configure()
    {
        Get("/api/documents/open/{companyKey}");
    }

    public override async Task HandleAsync(Request request, CancellationToken cancellationToken)
    {
        var company = Enum.Parse<Company>(
            request.CompanyKey,
            ignoreCase: true);

        var document = await repository.OpenReadAsync(
            company,
            request.BlobName,
            cancellationToken);

        if (document is null)
        {
            await Send.NotFoundAsync(cancellationToken);
            return;
        }

        await using var content = document.Content;

        await Send.StreamAsync(
            stream: content,
            fileName: Path.GetFileName(document.BlobName),
            fileLengthBytes: document.Length,
            contentType: document.ContentType,
            enableRangeProcessing: true,
            cancellation: cancellationToken);
    }
}
