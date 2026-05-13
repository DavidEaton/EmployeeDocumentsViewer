using FastEndpoints;

namespace EmployeeDocumentsViewer.Features.Documents.List;

public sealed class Endpoint(IDocumentRepository repository)
    : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Post("/api/documents/list");
    }

    public override async Task HandleAsync(Request request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<Company>(request.CompanyKey, true, out var company))
        {
            await Send.ErrorsAsync(statusCode: StatusCodes.Status400BadRequest, cancellation: cancellationToken);
            return;
        }

        var (totalCount, filteredCount, items) = await repository.SearchAsync(
            company,
            request.Page,
            request.Size,
            request.Filters,
            request.Sorters,
            cancellationToken);

        await Send.OkAsync(new Response
        {
            TotalCount = totalCount,
            FilteredCount = filteredCount,
            LastPage = Math.Max(1, (int)Math.Ceiling(filteredCount / (double)request.Size)),
            Data = items
        }, cancellation: cancellationToken);
    }
}
