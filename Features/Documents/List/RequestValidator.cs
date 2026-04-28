using FastEndpoints;
using FluentValidation;

namespace EmployeeDocumentsViewer.Features.Documents.List;

public sealed class RequestValidator : Validator<Request>
{
    public RequestValidator()
    {
        RuleFor(x => x.CompanyKey)
            .MustBeValidCompany();

        RuleFor(x => x.Draw)
            .GreaterThanOrEqualTo(0);

        RuleFor(x => x.Start)
            .GreaterThanOrEqualTo(0);

        RuleFor(x => x.Length)
            .InclusiveBetween(1, 100)
            .WithMessage("Length must be between 1 and 100.");
    }
}