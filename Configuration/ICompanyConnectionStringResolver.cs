using EmployeeDocumentsViewer.Features;

namespace EmployeeDocumentsViewer.Configuration;

public interface ICompanyConnectionStringResolver
{
    string GetSqlConnectionString(Company company);
    string GetBlobStorageConnectionString(Company company);
    IReadOnlyList<CompanyOption> GetAvailableCompanies();
}

public sealed record CompanyOption(
    Company Company,
    string DisplayName);