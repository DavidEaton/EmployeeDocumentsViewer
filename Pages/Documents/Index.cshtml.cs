using EmployeeDocumentsViewer.Configuration;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EmployeeDocumentsViewer.Pages.Documents;

public sealed class IndexModel(ICompanyConnectionStringResolver companyResolver) : PageModel
{
    public IReadOnlyList<SelectListItem> Companies { get; private set; } = [];

    public void OnGet() =>
        Companies = companyResolver.GetAvailableCompanies()
            .Select(option => new SelectListItem
            {
                Value = option.Company.ToString(),
                Text = option.DisplayName
            })
            .ToArray();
}