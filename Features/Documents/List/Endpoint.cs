using FastEndpoints;

namespace EmployeeDocumentsViewer.Features.Documents.List;

public sealed class Endpoint(IDocumentRepository repository)
    : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Post("/api/documents/read/list");
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

        var (totalCount, filteredCount, items) = await repository.SearchAsync(
            company,
            request.SearchTerm,
            request.SortColumn,
            request.SortDirection,
            request.Start,
            request.Length,
            cancellationToken);

        await Send.OkAsync(new Response
        {
            Draw = request.Draw,
            RecordsTotal = totalCount,
            RecordsFiltered = filteredCount,
            Data = [.. items.Select(record => record.ToReadRow(request.CompanyKey))]
        }, cancellationToken);
    }
}
