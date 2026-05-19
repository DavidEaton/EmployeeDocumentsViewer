using EmployeeDocumentsViewer.Configuration;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Options;

namespace EmployeeDocumentsViewer.Pages.Documents;

public sealed class IndexModel(
    ICompanyConnectionStringResolver companyResolver,
    IOptions<DocumentsPageOptions> options) : PageModel
{
    public IReadOnlyList<SelectListItem> Companies { get; private set; } = [];
    public string Title => options.Value.Title;
    public string Description => options.Value.Description;
    public string Message => options.Value.Message;

    public void OnGet() =>
        Companies = companyResolver.GetAvailableCompanies()
            .Select(option => new SelectListItem
            {
                Value = option.Company.ToString(),
                Text = option.DisplayName
            })
            .ToArray();
}