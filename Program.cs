using System.Diagnostics;
using System.Text.Json;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using EmployeeDocumentsViewer.Configuration;
using EmployeeDocumentsViewer.Features;
using EmployeeDocumentsViewer.Features.Documents;
using EmployeeDocumentsViewer.Features.Documents.Indexing;
using EmployeeDocumentsViewer.Security;
using FastEndpoints;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.Authentication;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>
{
    options.SingleLine = true;
    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff zzz ";
    options.IncludeScopes = true;
});
builder.Logging.AddDebug();

var applicationInsightsConnectionString =
    builder.Configuration.GetConnectionString("ApplicationInsights")
    ?? builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];

if (string.IsNullOrWhiteSpace(applicationInsightsConnectionString))
{
    throw new InvalidOperationException(
        "Application Insights connection string is missing. " +
        "Set ConnectionStrings:ApplicationInsights in user secrets or APPLICATIONINSIGHTS_CONNECTION_STRING.");
}

builder.Services.AddOpenTelemetry()
    .UseAzureMonitor(options =>
    {
        options.ConnectionString = builder.Environment.IsProduction()
            ? Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING")
                ?? applicationInsightsConnectionString
            : applicationInsightsConnectionString;
    });

builder.Services.AddRazorPages();
builder.Services.AddFastEndpoints();

builder.Services.SwaggerDocument(options =>
{
    options.DocumentSettings = settings =>
    {
        settings.Title = "Employee Documents Viewer API";
        settings.Version = "v1";
    };
});

builder.Services
    .AddAuthentication(DevAuthHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, DevAuthHandler>(
        DevAuthHandler.SchemeName,
        _ => { });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("InternalUsers", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireClaim("employee_portal", "true");
    });

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});

builder.Services.Configure<CompanyConnectionOptions>(
    builder.Configuration.GetSection(CompanyConnectionOptions.SectionName));

builder.Services.Configure<StorageOptions>(
    builder.Configuration.GetSection(StorageOptions.SectionName));

builder.Services.AddScoped<ICompanyConnectionStringResolver, CompanyConnectionStringResolver>();
builder.Services.AddScoped<IDocumentRepository, SqlDocumentRepository>();
builder.Services.AddScoped<IDocumentCatalogIndexer, SqlDocumentCatalogIndexer>();

var app = builder.Build();

await RunDocumentCatalogIndexingOnceAtStartupAsync(app);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.Use(async (context, next) =>
{
    var logger = context.RequestServices
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("RequestScope");

    using (logger.BeginScope(new Dictionary<string, object?>
    {
        ["TraceId"] = Activity.Current?.TraceId.ToString(),
        ["Path"] = context.Request.Path.Value,
        ["Method"] = context.Request.Method
    }))
    {
        await next();
    }
});

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.UseFastEndpoints();

if (app.Environment.IsDevelopment())
{
    app.UseSwaggerGen();
}

app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();

app.MapGet("/", () => Results.LocalRedirect("/documents"));

app.Run();

static async Task RunDocumentCatalogIndexingOnceAtStartupAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();

    var logger = scope.ServiceProvider
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("StartupIndexing");

    var indexer = scope.ServiceProvider.GetRequiredService<IDocumentCatalogIndexer>();

    logger.LogInformation("Starting one-time document catalog indexing.");

    foreach (var company in Enum.GetValues<Company>())
    {
        try
        {
            logger.LogInformation(
                "Indexing document catalog for company {Company}.",
                company);

            await indexer.SyncCompanyAsync(company, CancellationToken.None);

            logger.LogInformation(
                "Completed document catalog indexing for company {Company}.",
                company);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Document catalog indexing failed for company {Company}.",
                company);

            throw;
        }
    }

    logger.LogInformation("One-time document catalog indexing completed.");
}