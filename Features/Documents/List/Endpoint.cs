using FastEndpoints;

namespace EmployeeDocumentsViewer.Features.Documents.List;

public sealed class Endpoint(IDocumentRepository repository)
    : Endpoint<Request, Response>
{
    private readonly IDocumentRepository _repository = repository;

    public override void Configure()
    {
        Post("/api/documents/list");
    }

    public override async Task HandleAsync(Request request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<Company>(
                request.CompanyKey,
                ignoreCase: true,
                out var company))
        {
            await Send.ErrorsAsync(
                statusCode: StatusCodes.Status400BadRequest,
                cancellation: cancellationToken);

            return;
        }

        var sortColumn = DocumentSortParser.ParseOrDefault(request.SortColumn);
        var descending = DocumentSortParser.IsDescending(request.SortDirection);
        var length = Math.Clamp(request.Length, 1, 100);
        var start = Math.Max(0, request.Start);

        var (totalCount, filteredCount, items) = await _repository.SearchAsync(
            company,
            request.SearchTerm,
            sortColumn,
            descending,
            start,
            length,
            cancellationToken);

        var response = new Response
        {
            Draw = request.Draw,
            RecordsTotal = totalCount,
            RecordsFiltered = filteredCount,
            Data = items
        };

        await Send.OkAsync(response, cancellation: cancellationToken);
    }
}
