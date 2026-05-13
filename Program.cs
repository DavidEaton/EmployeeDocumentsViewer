using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using EmployeeDocumentsViewer.Configuration;
using EmployeeDocumentsViewer.Features.Documents;
using FastEndpoints;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

var configuredLogPath = builder.Environment.IsDevelopment()
    ? builder.Configuration["LogPath"]
    : Environment.GetEnvironmentVariable("BACKFILL_LOG_PATH");

var requestedLogPath = string.IsNullOrWhiteSpace(configuredLogPath)
    ? "/logs/log-.txt"
    : configuredLogPath;

string effectiveLogPath;

try
{
    EnsureLogDirectoryExists(requestedLogPath);
    effectiveLogPath = requestedLogPath;
}
catch (Exception ex)
{
    var fallbackLogPath = "/tmp/employee-documents-viewer/log-.txt";
    EnsureLogDirectoryExists(fallbackLogPath);
    effectiveLogPath = fallbackLogPath;

    Console.Error.WriteLine($"[EmployeeDocumentsViewer] Failed to initialize requested log path '{requestedLogPath}'. Falling back to '{fallbackLogPath}'. Error: {ex.Message}");
}

Console.WriteLine($"[EmployeeDocumentsViewer] Effective log path: {effectiveLogPath}");

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .MinimumLevel.Debug()
    .WriteTo.Console(
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.File(
        path: effectiveLogPath,
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14,
        shared: true,
        flushToDiskInterval: TimeSpan.FromSeconds(1),
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj} {Properties:j}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();
builder.Services.AddSerilog();

JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

var startupLogger = LoggerFactory.Create(logging => logging.AddSerilog(Log.Logger, dispose: false))
    .CreateLogger("Startup");

static bool HasRequiredAzureAdSettings(IConfiguration configuration)
{
    var azureAd = configuration.GetSection("AzureAd");

    var instance = azureAd["Instance"];
    var tenantId = azureAd["TenantId"];
    var clientId = azureAd["ClientId"];
    var clientSecret = azureAd["ClientSecret"];
    var callbackPath = azureAd["CallbackPath"];

    return !string.IsNullOrWhiteSpace(instance)
        && !string.IsNullOrWhiteSpace(tenantId)
        && !string.IsNullOrWhiteSpace(clientId)
        && !string.IsNullOrWhiteSpace(clientSecret)
        && !string.IsNullOrWhiteSpace(callbackPath);
}

static AuthorizationPolicy BuildDenyAllPolicy()
{
    return new AuthorizationPolicyBuilder()
        .RequireAssertion(_ => false)
        .Build();
}

static void EnsureLogDirectoryExists(string logPath)
{
    var directory = Path.GetDirectoryName(logPath);

    if (string.IsNullOrWhiteSpace(directory))
    {
        throw new InvalidOperationException($"Log path '{logPath}' must include a directory.");
    }

    Directory.CreateDirectory(directory);
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

builder.Services.Configure<DocumentsPageOptions>(
   builder.Configuration.GetSection("DocumentsPage"));

builder.Services.Configure<CompanyConnectionOptions>(
    builder.Configuration.GetSection(CompanyConnectionOptions.SectionName));

builder.Services.Configure<StorageOptions>(
    builder.Configuration.GetSection("Storage"));

builder.Services.AddScoped<ICompanyConnectionStringResolver, CompanyConnectionStringResolver>();
builder.Services.AddScoped<IDocumentRepository, SqlDocumentRepository>();

var app = builder.Build();

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

    app.MapGet("/debug/claims", (HttpContext ctx) =>
    {
        return Results.Json(ctx.User.Claims.Select(c => new { c.Type, c.Value }));
    });

    app.MapGet("/health", () => Results.Ok(new
    {
        status = "Healthy",
        authenticationConfigured = hasAzureAdConfiguration,
        authorizationConfigured = hasValidGroupConfiguration
    }));
}

app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();

app.MapGet("/", () => Results.LocalRedirect("/documents"));

app.Run();
