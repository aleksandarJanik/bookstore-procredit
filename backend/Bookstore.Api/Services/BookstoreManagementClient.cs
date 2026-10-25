using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Bookstore.Api.Contracts;
using Microsoft.Extensions.Caching.Memory;

namespace Bookstore.Api.Services;

public interface IBookstoreManagementClient
{
    Task<HttpResponseMessage> GetAuthorsAsync(CancellationToken cancellationToken);
    Task<HttpResponseMessage> CreateAsync(SaveBookRequest request, CancellationToken cancellationToken);
    Task<HttpResponseMessage> GetAsync(int bookId, CancellationToken cancellationToken);
    Task<HttpResponseMessage> DeleteAsync(int bookId, CancellationToken cancellationToken);
}

public sealed class BookstoreManagementClient(
    HttpClient http,
    IConfiguration configuration,
    IMemoryCache cache) : IBookstoreManagementClient
{
    private const string TokenCacheKey = "bookstore-management-token";

    public Task<HttpResponseMessage> GetAuthorsAsync(CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Get, "/api/authors", null, cancellationToken);

    public Task<HttpResponseMessage> CreateAsync(SaveBookRequest request, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Post, "/api/books", JsonContent.Create(request), cancellationToken);

    public Task<HttpResponseMessage> GetAsync(int bookId, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Get, $"/api/books/{bookId}", null, cancellationToken);

    public Task<HttpResponseMessage> DeleteAsync(int bookId, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Delete, $"/api/books/{bookId}", null, cancellationToken);

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        HttpContent? content,
        CancellationToken cancellationToken)
    {
        var apiBaseUrl = RequiredSetting("ManagementClient:ApiBaseUrl").TrimEnd('/');
        using var request = new HttpRequestMessage(method, $"{apiBaseUrl}{path}") { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await GetAccessTokenAsync(cancellationToken));
        return await http.SendAsync(request, cancellationToken);
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(TokenCacheKey, out string? cachedToken) && cachedToken is not null)
        {
            return cachedToken;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, RequiredSetting("ManagementClient:TokenEndpoint"))
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = RequiredSetting("ManagementClient:ClientId"),
                ["client_secret"] = RequiredSetting("ManagementClient:ClientSecret"),
                ["grant_type"] = "client_credentials",
                ["scope"] = "books.manage"
            })
        };
        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken)
            ?? throw new InvalidOperationException("The identity provider returned an empty token response.");
        cache.Set(TokenCacheKey, token.AccessToken, TimeSpan.FromSeconds(Math.Max(30, token.ExpiresIn - 30)));
        return token.AccessToken;
    }

    private string RequiredSetting(string key) => configuration[key]
        ?? throw new InvalidOperationException($"{key} is required for the management UI.");

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}
