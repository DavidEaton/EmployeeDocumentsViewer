using FluentValidation;

namespace EmployeeDocumentsViewer.Features.Documents;

public static class CompanyValidationRules
{
    public static IRuleBuilderOptions<T, string> MustBeValidCompany<T>(
        this IRuleBuilder<T, string> rule)
    {
        return rule
            .Must(x => Enum.TryParse<Company>(x, ignoreCase: true, out _))
            .WithMessage("Invalid company key.");
    }
}