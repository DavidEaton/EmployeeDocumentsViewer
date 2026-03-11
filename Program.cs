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

builder.Services.AddSingleton<IDocumentRepository, SqlDocumentRepository>();
var companyConnections = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
{
    ["CompanyA"] = builder.Configuration.GetConnectionString("CompanyA")
        ?? throw new InvalidOperationException("Missing connection string: CompanyA"),
    ["CompanyB"] = builder.Configuration.GetConnectionString("CompanyB")
        ?? throw new InvalidOperationException("Missing connection string: CompanyB"),
    ["CompanyC"] = builder.Configuration.GetConnectionString("CompanyC")
        ?? throw new InvalidOperationException("Missing connection string: CompanyC"),
    ["CompanyD"] = builder.Configuration.GetConnectionString("CompanyD")
        ?? throw new InvalidOperationException("Missing connection string: CompanyD")
};

// builder.Services.AddScoped<IDocumentRepository>(
//     _ => new SqlDocumentRepository(companyConnections));
    
var app = builder.Build();
// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

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
app.MapRazorPages()
   .WithStaticAssets();

app.MapGet("/", () => Results.LocalRedirect("/documents"));

app.Run();
