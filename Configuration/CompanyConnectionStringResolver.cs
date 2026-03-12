using EmployeeDocumentsViewer.Features;
using Microsoft.Extensions.Options;

namespace EmployeeDocumentsViewer.Configuration;

public sealed class CompanyConnectionStringResolver(
    IOptions<CompanyConnectionOptions> options)
    : ICompanyConnectionStringResolver
{
    private readonly CompanyConnectionOptions _options = options.Value;

    public string GetConnectionString(Company company)
    {
        var key = company.ToString();

        if (!_options.Companies.TryGetValue(key, out var item))
            throw new InvalidOperationException(
                $"No company connection configuration exists for company '{key}'.");

        if (string.IsNullOrWhiteSpace(item.ConnectionString))
            throw new InvalidOperationException(
                $"Connection string for company '{key}' is missing.");

        return item.ConnectionString;
    }

    public IReadOnlyList<CompanyOption> GetAvailableCompanies() =>
        [.. Enum.GetValues<Company>()
            .Select(company =>
            {
                var key = company.ToString();

                return _options.Companies.TryGetValue(key, out var item)
                    && !string.IsNullOrWhiteSpace(item.DisplayName)
                    ? new CompanyOption(company, item.DisplayName)
                    : new CompanyOption(company, key);
            })];
}