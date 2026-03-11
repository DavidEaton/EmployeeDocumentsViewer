using EmployeeDocumentsViewer.Features;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EmployeeDocumentsViewer.Pages.Documents;

public sealed class IndexModel : PageModel
{
    public IReadOnlyList<SelectListItem> Companies { get; private set; } = [];

    public void OnGet() =>
        Companies = Enum.GetValues<Company>()
            .Select(company =>
            new SelectListItem
            {
                Value = company.ToString(),
                Text = company.ToString()
            })
            .ToArray();
}