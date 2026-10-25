using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bookstore.Api.Tests;

public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Test-Auth", out var mode))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = mode.ToString() switch
        {
            "manage" => new[] { new Claim("scope", "books.manage"), new Claim("azp", "bookstore-service") },
            "search" => new[] { new Claim("scope", "books.search"), new Claim("azp", "bookstore-ui") },
            "editor" => new[]
            {
                new Claim("scope", "books.search"),
                new Claim("azp", "bookstore-ui"),
                new Claim("realm_access", "{\"roles\":[\"bookstore-editor\"]}")
            },
            _ => Array.Empty<Claim>()
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
