using FastEndpoints;
using FluentValidation;

namespace EmployeeDocumentsViewer.Features.Documents.Open;

public sealed class RequestValidator : Validator<Request>
{
    public RequestValidator()
    {
        RuleFor(x => x.CompanyKey)
            .MustBeValidCompany();

        RuleFor(x => x.BlobName)
            .NotEmpty()
            .WithMessage("Missing blobName query string value.");
    }
}