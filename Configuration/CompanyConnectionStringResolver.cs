using EmployeeDocumentsViewer.Features;
using Microsoft.Extensions.Options;

namespace EmployeeDocumentsViewer.Configuration;

public sealed class CompanyConnectionStringResolver(
    IOptions<CompanyConnectionOptions> options)
    : ICompanyConnectionStringResolver
{
    private readonly CompanyConnectionOptions _options = options.Value;

    public string GetSqlConnectionString(Company company)
    {
        var item = GetCompanyItem(company);

        return string.IsNullOrWhiteSpace(item.ConnectionString)
            ? throw new InvalidOperationException(
                $"SQL connection string for company '{company}' is missing.")
            : item.ConnectionString;
    }

    public string GetBlobStorageConnectionString(Company company)
    {
        var item = GetCompanyItem(company);

        return string.IsNullOrWhiteSpace(item.BlobStorageConnectionString)
            ? throw new InvalidOperationException(
                $"Blob storage connection string for company '{company}' is missing.")
            : item.BlobStorageConnectionString;
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

    private CompanyConnectionItem GetCompanyItem(Company company)
    {
        var key = company.ToString();

        return !_options.Companies.TryGetValue(key, out var item)
            ? throw new InvalidOperationException(
                $"No company connection configuration exists for company '{key}'.")
            : item;
    }
}
