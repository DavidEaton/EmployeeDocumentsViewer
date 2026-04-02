using FastEndpoints;

namespace EmployeeDocumentsViewer.Features.Documents.List;

public sealed class Endpoint(IDocumentRepository repository)
    : Endpoint<Request, Response>
{
    private readonly IDocumentRepository _repository = repository;

    public override void Configure()
    {
        Post("/api/documents/list");
        Policies("InternalUsers");
    }

    public override async Task HandleAsync(Request request, CancellationToken cancellationToken)
    {
        var company = Enum.Parse<Company>(request.CompanyKey, ignoreCase: true);

        var sortColumn = DocumentSortParser.ParseOrDefault(request.SortColumn);
        var descending = DocumentSortParser.IsDescending(request.SortDirection);

        var (totalCount, filteredCount, items) = await _repository.SearchAsync(
            company,
            request.SearchTerm,
            sortColumn,
            descending,
            request.Start,
            request.Length,
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