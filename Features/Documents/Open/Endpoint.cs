using FastEndpoints;

namespace EmployeeDocumentsViewer.Features.Documents.Open;

public sealed class Endpoint(IDocumentRepository repository)
    : Endpoint<Request>
{
    public override void Configure()
    {
        Get("/api/documents/open/{companyKey}");
        Policies("InternalUsers");
    }

    public override async Task HandleAsync(Request request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<Company>(request.CompanyKey, ignoreCase: true, out var company))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsJsonAsync(
                new { error = $"Invalid company key '{request.CompanyKey}'." },
                cancellationToken);
            return;
        }

        if (string.IsNullOrWhiteSpace(request.BlobName))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsJsonAsync(
                new { error = "Missing blobName query string value." },
                cancellationToken);
            return;
        }

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
