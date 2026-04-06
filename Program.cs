using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using EmployeeDocumentsViewer.Configuration;
using EmployeeDocumentsViewer.Features.Documents;
using FastEndpoints;
using FastEndpoints.Swagger;
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

// Prevent legacy inbound claim remapping.
// This keeps claim names such as "groups" and "name" in their token form.
JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

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

var hrEmployeeDocumentsGroupId =
    builder.Configuration["Authorization:HREmployeeDocumentsGroupId"];

if (string.IsNullOrWhiteSpace(hrEmployeeDocumentsGroupId))
{
    throw new InvalidOperationException(
        "Authorization:HREmployeeDocumentsGroupId is missing. " +
        "Set it to the Microsoft Entra object ID of the HREmployeeDocuments security group.");
}

builder.Services
    .AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"));

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("HREmployeeDocumentsOnly", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireClaim("groups", hrEmployeeDocumentsGroupId);
    });

    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .RequireClaim("groups", hrEmployeeDocumentsGroupId)
        .Build();
});

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
app.MapGet("/debug/claims", (HttpContext ctx) =>
{
    return ctx.User.Claims.Select(c => new { c.Type, c.Value });
});
app.Run();