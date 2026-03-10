using System.Text;

namespace EmployeeDocumentsViewer.Features.Documents;

public sealed class InMemoryDocumentRepository : IDocumentRepository
{
    private readonly IReadOnlyList<DocumentRecord> _documents =
    [
        new(
            1,
            "Alice Carter",
            "HR",
            "Policy",
            2026,
            "alice-carter-policy.pdf",
            CreateMinimalPdf("Alice Carter - HR Policy")),

        new(
            2,
            "Bob Evans",
            "Finance",
            "Contract",
            2025,
            "bob-evans-contract.pdf",
            CreateMinimalPdf("Bob Evans - Finance Contract")),

        new(
            3,
            "Carla Jones",
            "IT",
            "Handbook",
            2024,
            "carla-jones-handbook.pdf",
            CreateMinimalPdf("Carla Jones - IT Handbook")),

        new(
            4,
            "David Smith",
            "HR",
            "Benefits Guide",
            2026,
            "david-smith-benefits-guide.pdf",
            CreateMinimalPdf("David Smith - Benefits Guide"))
    ];

    public Task<(int TotalCount, int FilteredCount, IReadOnlyList<DocumentRecord> Items)> SearchAsync(
        string? searchTerm,
        string? sortColumn,
        string? sortDirection,
        int start,
        int length,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IEnumerable<DocumentRecord> query = _documents;

        var total = _documents.Count;

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();

            query = query.Where(x =>
                x.Employee.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                x.Department.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                x.DocumentType.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                x.Year.ToString().Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        var filtered = query.Count();

        query = ApplySorting(query, sortColumn, sortDirection);

        var items = query
            .Skip(Math.Max(0, start))
            .Take(length <= 0 ? 10 : length)
            .ToArray();

        return Task.FromResult((total, filtered, (IReadOnlyList<DocumentRecord>)items));
    }

    public Task<DocumentRecord?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var item = _documents.FirstOrDefault(x => x.Id == id);
        return Task.FromResult(item);
    }

    private static IEnumerable<DocumentRecord> ApplySorting(
        IEnumerable<DocumentRecord> query,
        string? sortColumn,
        string? sortDirection)
    {
        var descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);

        return (sortColumn ?? string.Empty).ToLowerInvariant() switch
        {
            "employee" => descending ? query.OrderByDescending(x => x.Employee) : query.OrderBy(x => x.Employee),
            "department" => descending ? query.OrderByDescending(x => x.Department) : query.OrderBy(x => x.Department),
            "documenttype" => descending ? query.OrderByDescending(x => x.DocumentType) : query.OrderBy(x => x.DocumentType),
            "year" => descending ? query.OrderByDescending(x => x.Year) : query.OrderBy(x => x.Year),
            _ => query.OrderBy(x => x.Employee)
        };
    }

    private static byte[] CreateMinimalPdf(string text)
    {
        // Minimal valid-ish PDF for demo purposes.
        // Good enough to prove the endpoint shape in browser/PDF viewer.
        var safeText = text.Replace("(", "[").Replace(")", "]");

        var pdf = $"""
%PDF-1.1
1 0 obj
<< /Type /Catalog /Pages 2 0 R >>
endobj
2 0 obj
<< /Type /Pages /Kids [3 0 R] /Count 1 >>
endobj
3 0 obj
<< /Type /Page /Parent 2 0 R /MediaBox [0 0 500 200] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>
endobj
4 0 obj
<< /Length {safeText.Length + 31} >>
stream
BT
/F1 18 Tf
50 120 Td
({safeText}) Tj
ET
endstream
endobj
5 0 obj
<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>
endobj
xref
0 6
0000000000 65535 f
0000000010 00000 n
0000000063 00000 n
0000000122 00000 n
0000000248 00000 n
0000000350 00000 n
trailer
<< /Root 1 0 R /Size 6 >>
startxref
420
%%EOF
""";

        return Encoding.ASCII.GetBytes(pdf);
    }
}