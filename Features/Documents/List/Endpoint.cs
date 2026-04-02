using FastEndpoints;

namespace EmployeeDocumentsViewer.Features.Documents.List
{
    public sealed class Endpoint(
        IDocumentRepository repository,
        ILogger<Endpoint> logger)
        : Endpoint<Request, Response>
    {
        public override void Configure()
        {
            Post("/api/documents/read/list");
            Policies("InternalUsers");
        }

        public override async Task HandleAsync(Request request, CancellationToken cancellationToken)
        {
            using var scope = logger.BeginScope(new Dictionary<string, object?>
            {
                ["CompanyKey"] = request.CompanyKey,
                ["Draw"] = request.Draw
            });

            if (!Enum.TryParse<Company>(request.CompanyKey, ignoreCase: true, out var company))
            {
                logger.LogWarning("Invalid company key supplied to document list endpoint: {CompanyKey}", request.CompanyKey);
                await Send.NotFoundAsync(cancellationToken);
                return;
            }

            logger.LogInformation(
                "Handling document list request for company {Company}. Start={Start}, Length={Length}.",
                company, request.Start, request.Length);

            var (totalCount, filteredCount, items) = await repository.SearchAsync(
                company,
                request.SearchTerm,
                request.SortColumn,
                request.SortDirection,
                request.Start,
                request.Length,
                cancellationToken);

            logger.LogInformation(
                "Document list request completed for company {Company}. Total={TotalCount}, Filtered={FilteredCount}, Returned={ReturnedCount}.",
                company, totalCount, filteredCount, items.Count);

            await Send.OkAsync(new Response
            {
                Draw = request.Draw,
                RecordsTotal = totalCount,
                RecordsFiltered = filteredCount,
                Data = items.Select(x => x.ToReadRow(request.CompanyKey)).ToArray()
            }, cancellationToken);
        }
    }
}