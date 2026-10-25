using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Bookstore.Api.Contracts;
using Bookstore.Api.Services;
using System.Net;
using System.Net.Http.Json;

namespace Bookstore.Api.Tests;

public sealed class TestWebApplicationFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:Bookstore"] = connectionString,
                ["Database:ApplyMigrations"] = "true",
                ["Database:SeedData"] = "false"
            }));

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IBookstoreManagementClient>();
            services.AddSingleton<IBookstoreManagementClient, FakeManagementClient>();
            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                    options.DefaultForbidScheme = TestAuthHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        });
    }

    private sealed class FakeManagementClient : IBookstoreManagementClient
    {
        public Task<HttpResponseMessage> GetAuthorsAsync(CancellationToken cancellationToken) =>
            Json(HttpStatusCode.OK, new[] { new AuthorResponse(77, "UI Author") });

        public Task<HttpResponseMessage> CreateAsync(SaveBookRequest request, CancellationToken cancellationToken) =>
            Json(HttpStatusCode.Created, new BookResponse(
                7001,
                new AuthorResponse(request.Author.AuthorId ?? 78, request.Author.Name),
                request.Title,
                request.SubTitle));

        public Task<HttpResponseMessage> GetAsync(int bookId, CancellationToken cancellationToken) =>
            Json(HttpStatusCode.OK, new BookResponse(
                bookId,
                new AuthorResponse(77, "UI Author"),
                "UI Book",
                null));

        public Task<HttpResponseMessage> DeleteAsync(int bookId, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));

        private static Task<HttpResponseMessage> Json<T>(HttpStatusCode statusCode, T value) =>
            Task.FromResult(new HttpResponseMessage(statusCode) { Content = JsonContent.Create(value) });
    }
}
