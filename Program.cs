using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using EmployeeDocumentsViewer.Configuration;
using EmployeeDocumentsViewer.Features.Documents;
using FastEndpoints;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>
{
    options.SingleLine = true;
    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff zzz ";
    options.IncludeScopes = true;
});
builder.Logging.AddDebug();

JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

var startupLogger = LoggerFactory.Create(logging =>
{
    logging.ClearProviders();
    logging.AddSimpleConsole(options =>
    {
        options.SingleLine = true;
        options.TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff zzz ";
        options.IncludeScopes = true;
    });
    logging.AddDebug();
}).CreateLogger("Startup");

static bool HasRequiredAzureAdSettings(IConfiguration configuration)
{
    var azureAd = configuration.GetSection("AzureAd");

    var instance = azureAd["Instance"];
    var tenantId = azureAd["TenantId"];
    var clientId = azureAd["ClientId"];
    var callbackPath = azureAd["CallbackPath"];

    return !string.IsNullOrWhiteSpace(instance)
        && !string.IsNullOrWhiteSpace(tenantId)
        && !string.IsNullOrWhiteSpace(clientId)
        && !string.IsNullOrWhiteSpace(callbackPath);
}

static AuthorizationPolicy BuildDenyAllPolicy()
{
    return new AuthorizationPolicyBuilder()
        .RequireAssertion(_ => false)
        .Build();
}

var applicationInsightsConnectionString =
    builder.Configuration.GetConnectionString("ApplicationInsights")
    ?? builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];

if (IsValidAppInsightsConnectionString(applicationInsightsConnectionString))
{
    builder.Services.AddOpenTelemetry()
        .UseAzureMonitor(options =>
        {
            options.ConnectionString = applicationInsightsConnectionString;
        });

    startupLogger.LogInformation("Application Insights telemetry enabled.");
}
else if (!string.IsNullOrWhiteSpace(applicationInsightsConnectionString))
{
    startupLogger.LogError(
        "Application Insights connection string is INVALID. Telemetry is disabled.");
}
else
{
    startupLogger.LogWarning(
        "Application Insights connection string not configured. Telemetry is disabled.");
}

static bool IsValidAppInsightsConnectionString(string? connectionString)
{
    if (string.IsNullOrWhiteSpace(connectionString))
        return false;

    if (!connectionString.Contains("InstrumentationKey=", StringComparison.OrdinalIgnoreCase))
        return false;

    var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);

    var ingestion = parts
        .FirstOrDefault(p => p.StartsWith("IngestionEndpoint=", StringComparison.OrdinalIgnoreCase));

    if (ingestion is not null)
    {
        var value = ingestion.Split('=', 2)[1];

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
            return false;

        if (uri.Scheme != Uri.UriSchemeHttps)
            return false;
    }

    return true;
}

var hrEmployeeDocumentsGroupId =
    builder.Configuration["Authorization:HREmployeeDocumentsGroupId"];

var hasValidGroupConfiguration = !string.IsNullOrWhiteSpace(hrEmployeeDocumentsGroupId);
if (!hasValidGroupConfiguration)
{
    startupLogger.LogCritical(
        "Authorization:HREmployeeDocumentsGroupId is missing. The application will start in deny-all mode.");
}

var hasAzureAdConfiguration = HasRequiredAzureAdSettings(builder.Configuration);
if (!hasAzureAdConfiguration)
{
    startupLogger.LogCritical(
        "AzureAd configuration is incomplete. The application will start in deny-all mode.");
}

if (hasAzureAdConfiguration)
{
    builder.Services
        .AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
        .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"));

    startupLogger.LogInformation("Microsoft Entra authentication enabled.");
}
else
{
    builder.Services
        .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie();

    startupLogger.LogWarning(
        "Microsoft Entra authentication was not configured. A local cookie scheme was registered only to satisfy middleware requirements. Authorization will deny all requests.");
}

builder.Services.AddAuthorizationBuilder();

if (hasAzureAdConfiguration && hasValidGroupConfiguration)
{
    builder.Services.AddAuthorizationBuilder()
        .AddPolicy("HREmployeeDocumentsOnly", policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireClaim("groups", hrEmployeeDocumentsGroupId!);
        })
        .SetFallbackPolicy(new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .RequireClaim("groups", hrEmployeeDocumentsGroupId!)
            .Build());

    startupLogger.LogInformation(
        "Authorization policy 'HREmployeeDocumentsOnly' enabled for group object ID {GroupId}.",
        hrEmployeeDocumentsGroupId);
}
else
{
    var denyAllPolicy = BuildDenyAllPolicy();

    builder.Services.AddAuthorizationBuilder()
        .AddPolicy("HREmployeeDocumentsOnly", _ => { })
        .SetFallbackPolicy(denyAllPolicy);

    startupLogger.LogWarning(
        "Authorization is running in deny-all mode because required security configuration is missing.");
}

builder.Services.AddRazorPages()
    .AddMicrosoftIdentityUI();

builder.Services.AddFastEndpoints();

builder.Services.SwaggerDocument(options =>
{
    options.DocumentSettings = settings =>
    {
        settings.Title = "Employee Documents Viewer API";
        settings.Version = "v1";
    };
});

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);

builder.Services.Configure<CompanyConnectionOptions>(
    builder.Configuration.GetSection(CompanyConnectionOptions.SectionName));

builder.Services.Configure<StorageOptions>(
    builder.Configuration.GetSection(StorageOptions.SectionName));

builder.Services.AddScoped<ICompanyConnectionStringResolver, CompanyConnectionStringResolver>();
builder.Services.AddScoped<IDocumentRepository, SqlDocumentRepository>();

var app = builder.Build();

var runningInContainer =
    string.Equals(
        Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"),
        "true",
        StringComparison.OrdinalIgnoreCase);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStatusCodePages(async statusCodeContext =>
{
    var response = statusCodeContext.HttpContext.Response;

    if (response.StatusCode == StatusCodes.Status401Unauthorized)
    {
        response.ContentType = "text/plain; charset=utf-8";
        await response.WriteAsync("Unauthorized. Sign-in is required.");
        return;
    }

    if (response.StatusCode == StatusCodes.Status403Forbidden)
    {
        response.ContentType = "text/plain; charset=utf-8";
        await response.WriteAsync("Forbidden. Access is denied. Verify Microsoft Entra sign-in and group membership.");
    }
});

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

app.MapGet("/debug/claims", (HttpContext ctx) =>
{
    return Results.Json(ctx.User.Claims.Select(c => new { c.Type, c.Value }));
})
.AllowAnonymous();

app.MapGet("/health", () => Results.Ok(new
{
    status = "Healthy",
    authenticationConfigured = hasAzureAdConfiguration,
    authorizationConfigured = hasValidGroupConfiguration,
    telemetryConfigured = !string.IsNullOrWhiteSpace(applicationInsightsConnectionString)
}))
.AllowAnonymous();

app.Run();