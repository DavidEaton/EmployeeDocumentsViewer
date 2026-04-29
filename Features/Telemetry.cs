using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace EmployeeDocumentsViewer.Features;

public static class Telemetry
{
    public const string ServiceName = "EmployeeDocumentsViewer";
    public const string ActivitySourceName = "EmployeeDocumentsViewer.Documents";
    public const string MeterName = "EmployeeDocumentsViewer.Documents";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    public static readonly Meter Meter = new(MeterName);

    public static readonly Counter<long> SearchRequests =
        Meter.CreateCounter<long>("documents.search.requests");

    public static readonly Counter<long> OpenRequests =
        Meter.CreateCounter<long>("documents.open.requests");

    public static readonly Counter<long> OpenNotFound =
        Meter.CreateCounter<long>("documents.open.not_found");
}
