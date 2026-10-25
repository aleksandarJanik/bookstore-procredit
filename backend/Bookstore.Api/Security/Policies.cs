using Microsoft.AspNetCore.Authorization;

namespace Bookstore.Api.Security;

public static class Policies
{
    public const string SearchBooks = "books.search";
    public const string ManageBooks = "books.manage";
    public const string UseManagementUi = "books.ui.manage";
}

public static class AuthorizationPolicyBuilderExtensions
{
    public static AuthorizationPolicyBuilder RequireScope(
        this AuthorizationPolicyBuilder policy,
        string requiredScope) => policy.RequireAssertion(context =>
            context.User.FindAll("scope")
                .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                .Contains(requiredScope, StringComparer.Ordinal));

    public static AuthorizationPolicyBuilder RequireRealmRole(
        this AuthorizationPolicyBuilder policy,
        string requiredRole) => policy.RequireAssertion(context =>
            context.User.FindAll("realm_access").Any(claim => HasRole(claim.Value, requiredRole)));

    private static bool HasRole(string realmAccess, string requiredRole)
    {
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(realmAccess);
            return document.RootElement.TryGetProperty("roles", out var roles)
                && roles.EnumerateArray().Any(role => role.GetString() == requiredRole);
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }
}
