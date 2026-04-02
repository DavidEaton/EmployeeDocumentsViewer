using EmployeeDocumentsViewer.Configuration;
using EmployeeDocumentsViewer.Features.Documents;
using EmployeeDocumentsViewer.Security;
using FastEndpoints;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.Authentication;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using System.Text.Json;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>
{
    options.SingleLine = true;
    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff zzz ";
    options.IncludeScopes = true;
});
builder.Logging.AddDebug();
// Temporary verification
var aiConnectionString = builder.Configuration.GetConnectionString("ApplicationInsights")
    ?? builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];

if (string.IsNullOrWhiteSpace(aiConnectionString))
{
    throw new InvalidOperationException(
        "Application Insights connection string is missing. " +
        "Set ConnectionStrings:ApplicationInsights in user secrets or APPLICATIONINSIGHTS_CONNECTION_STRING.");
}

builder.Services.AddOpenTelemetry()
    .UseAzureMonitor(options =>
    {
        // Option 1: Use appsettings.json
        options.ConnectionString = builder.Configuration.GetConnectionString("ApplicationInsights");
        // Option 2: Or environment variable in Azure
        // APPLICATIONINSIGHTS_CONNECTION_STRING
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
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);

builder.Services.Configure<CompanyConnectionOptions>(
    builder.Configuration.GetSection(CompanyConnectionOptions.SectionName));

builder.Services.Configure<StorageOptions>(
    builder.Configuration.GetSection(StorageOptions.SectionName));

builder.Services.AddSingleton<ICompanyConnectionStringResolver, CompanyConnectionStringResolver>();
builder.Services.AddSingleton<IDocumentRepository, SqlDocumentRepository>();

var app = builder.Build();

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