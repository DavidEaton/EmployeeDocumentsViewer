using FastEndpoints;
using FluentValidation;

namespace EmployeeDocumentsViewer.Features.Documents.List;

public sealed class RequestValidator : Validator<Request>
{
    public RequestValidator()
    {
        RuleFor(x => x.CompanyKey).MustBeValidCompany();
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.Size).InclusiveBetween(1, 100);
    }
}
