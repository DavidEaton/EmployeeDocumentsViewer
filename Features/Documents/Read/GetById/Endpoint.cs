using FastEndpoints;

namespace EmployeeDocumentsViewer.Features.Documents.Read.GetById;
public sealed class Endpoint(IDocumentRepository repository)
    : Endpoint<Request>
{
    public override void Configure()
    {
        Get("/api/documents/open/{companyKey}/{id:int}");
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

        var document = await repository.GetByIdAsync(company, request.Id, cancellationToken);

        if (document is null)
        {
            await Send.NotFoundAsync(cancellationToken);
            return;
        }

        await Send.StreamAsync(
            stream: new MemoryStream(document.PdfBytes, writable: false),
            fileName: null,
            fileLengthBytes: document.PdfBytes.Length,
            contentType: "application/pdf",
            enableRangeProcessing: true,
            cancellation: cancellationToken);
    }   
}