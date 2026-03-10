using FastEndpoints;

namespace EmployeeDocumentsViewer.Features.Documents.Read.List;

public sealed class Endpoint(IDocumentRepository repository)
    : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Post("/api/documents/read/list");
        Policies("InternalUsers");
    }

    public override async Task HandleAsync(Request request, CancellationToken ct)
    {
        var (totalCount, filteredCount, items) = await repository.SearchAsync(
            request.SearchTerm,
            request.SortColumn,
            request.SortDirection,
            request.Start,
            request.Length,
            ct);

        var response = new Response
        {
            Draw = request.Draw,
            RecordsTotal = totalCount,
            RecordsFiltered = filteredCount,
            Data = items.Select(x => x.ToReadRow()).ToArray()
        };

        await Send.OkAsync(response, ct);
    }
}