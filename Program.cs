using EmployeeDocumentsViewer.Features.Documents;
using EmployeeDocumentsViewer.Security;
using FastEndpoints;
using Microsoft.AspNetCore.Authentication;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddFastEndpoints();

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

builder.Services.AddSingleton<IDocumentRepository, InMemoryDocumentRepository>();

var app = builder.Build();
// Configure the HTTP request pipeline.
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
app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.MapGet("/", () => Results.LocalRedirect("/documents"));

app.Run();
