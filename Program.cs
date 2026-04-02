using EmployeeDocumentsViewer.Configuration;
using EmployeeDocumentsViewer.Features.Documents;
using EmployeeDocumentsViewer.Security;
using FastEndpoints;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.Authentication;

var builder = WebApplication.CreateBuilder(args);

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
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});

builder.Services.Configure<CompanyConnectionOptions>(
    builder.Configuration.GetSection(CompanyConnectionOptions.SectionName));

builder.Services.Configure<StorageOptions>(
    builder.Configuration.GetSection(StorageOptions.SectionName));

var companyOptions = builder.Configuration.GetSection("CompanyConnections").Get<CompanyConnectionOptions>();

foreach (var kvp in companyOptions?.Companies ?? [])
{
    var key = kvp.Key;
    var item = kvp.Value;

    Console.WriteLine(
        $"{key}: SQL={!string.IsNullOrWhiteSpace(item.ConnectionString)}, " +
        $"Blob={!string.IsNullOrWhiteSpace(item.BlobStorageConnectionString)}, " +
        $"DisplayName={item.DisplayName}");
}


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