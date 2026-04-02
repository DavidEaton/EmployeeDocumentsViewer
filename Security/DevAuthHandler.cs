using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace EmployeeDocumentsViewer.Security;

public sealed class DevAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, loggerFactory, encoder)
{
    public const string SchemeName = "DevAuth";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        Logger.LogDebug("Authenticating request using {SchemeName}. Path={Path}", SchemeName, Request.Path);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "dev-user-1"),
            new Claim(ClaimTypes.Name, "David Demo"),
            new Claim(ClaimTypes.Email, "david.demo@company.local"),
            new Claim("employee_portal", "true")
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        Logger.LogDebug("Development authentication succeeded for {UserName}.", principal.Identity?.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}