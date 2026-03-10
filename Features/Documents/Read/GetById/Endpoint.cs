using FastEndpoints;

namespace EmployeeDocumentsViewer.Features.Documents.Read.GetById;

public sealed class Endpoint(IDocumentRepository repository)
    : Endpoint<Request>
{
    public override void Configure()
    {
        Get("/documents/open/{id:int}");
        Policies("InternalUsers");
    }

    public override async Task HandleAsync(Request request, CancellationToken ct)
    {
        var document = await repository.GetByIdAsync(request.Id, ct);

        if (document is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var stream = new MemoryStream(document.PdfBytes, writable: false);

        await Send.StreamAsync(
            stream: stream,
            fileName: null,
            fileLengthBytes: document.PdfBytes.Length,
            contentType: "application/pdf",
            enableRangeProcessing: true,
            cancellation: ct);
    }
}