using System.Net;
using System.Net.Http.Json;
using Bookstore.Api.Contracts;
using Bookstore.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;

namespace Bookstore.Api.Tests;

public sealed class BooksApiTests : IAsyncLifetime
{
    private readonly MsSqlContainer database =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
    private TestWebApplicationFactory factory = null!;

    public async Task InitializeAsync()
    {
        await database.StartAsync();
        factory = new TestWebApplicationFactory(database.GetConnectionString());
    }

    public async Task DisposeAsync()
    {
        await factory.DisposeAsync();
        await database.DisposeAsync();
    }

    [Fact]
    public async Task Protected_endpoints_require_authentication_and_the_correct_scope()
    {
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/books/search")).StatusCode);

        using var searchClient = CreateClient("search");
        var response = await searchClient.PostAsJsonAsync("/api/books", NewBook(null, "Octavia Butler", "Kindred"));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Crud_lifecycle_returns_the_expected_contract()
    {
        using var client = CreateClient("manage");
        var create = await client.PostAsJsonAsync("/api/books", NewBook(null, "Ursula Le Guin", "A Wizard of Earthsea"));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<BookResponse>();
        Assert.NotNull(created);
        Assert.True(created.BookId > 0);

        var get = await client.GetFromJsonAsync<BookResponse>($"/api/books/{created.BookId}");
        Assert.Equal("A Wizard of Earthsea", get?.Title);

        var update = await client.PutAsJsonAsync(
            $"/api/books/{created.BookId}",
            NewBook(created.Author.AuthorId, "Ursula Le Guin", "The Dispossessed"));
        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/books/{created.BookId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/books/{created.BookId}")).StatusCode);
    }

    [Fact]
    public async Task Search_is_case_insensitive_combines_filters_and_paginates()
    {
        using var manager = CreateClient("manage");
        var morrisonResponse = await manager.PostAsJsonAsync("/api/books", NewBook(null, "Toni Morrison", "Beloved"));
        var morrison = await morrisonResponse.Content.ReadFromJsonAsync<BookResponse>();
        Assert.NotNull(morrison);
        await manager.PostAsJsonAsync("/api/books", NewBook(morrison.Author.AuthorId, "Toni Morrison", "The Bluest Eye"));
        await manager.PostAsJsonAsync("/api/books", NewBook(null, "James Baldwin", "The Fire Next Time"));

        using var search = CreateClient("search");
        var result = await search.GetFromJsonAsync<PagedResult<BookResponse>>(
            "/api/books/search?title=E&author=MORRISON&page=1&pageSize=1");

        Assert.NotNull(result);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
        Assert.Single(result.Items);
        Assert.Equal("Toni Morrison", result.Items[0].Author.Name);
    }

    [Fact]
    public async Task Existing_author_id_with_a_different_name_returns_conflict()
    {
        using var client = CreateClient("manage");
        var create = await client.PostAsJsonAsync("/api/books", NewBook(null, "N. K. Jemisin", "The Fifth Season"));
        var created = await create.Content.ReadFromJsonAsync<BookResponse>();
        Assert.NotNull(created);

        var conflict = await client.PostAsJsonAsync(
            "/api/books",
            NewBook(created.Author.AuthorId, "Different Name", "The Obelisk Gate"));
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
    }

    [Fact]
    public async Task Whitespace_only_required_text_is_rejected()
    {
        using var client = CreateClient("manage");
        var response = await client.PostAsJsonAsync("/api/books", NewBook(null, "Valid Author", "   "));
        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest,
            $"Expected 400 but received {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    }

    [Fact]
    public async Task Author_ids_are_generated_and_existing_authors_can_be_listed_and_reused()
    {
        using var client = CreateClient("manage");
        var firstResponse = await client.PostAsJsonAsync("/api/books", NewBook(null, "Generated Author", "First Book"));
        var first = await firstResponse.Content.ReadFromJsonAsync<BookResponse>();
        Assert.NotNull(first);
        Assert.True(first.Author.AuthorId > 0);

        var authors = await client.GetFromJsonAsync<AuthorResponse[]>("/api/authors");
        Assert.Contains(authors!, author => author == first.Author);

        var secondResponse = await client.PostAsJsonAsync(
            "/api/books",
            NewBook(first.Author.AuthorId, first.Author.Name, "Second Book"));
        var second = await secondResponse.Content.ReadFromJsonAsync<BookResponse>();
        Assert.Equal(first.Author.AuthorId, second?.Author.AuthorId);

        var unknown = await client.PostAsJsonAsync("/api/books", NewBook(int.MaxValue, "Unknown Author", "Third Book"));
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
    }

    [Fact]
    public async Task Identity_migration_preserves_existing_authors_and_books()
    {
        using var client = CreateClient("manage");
        var createdResponse = await client.PostAsJsonAsync("/api/books", NewBook(null, "Migration Author", "Migration Book"));
        var created = await createdResponse.Content.ReadFromJsonAsync<BookResponse>();
        Assert.NotNull(created);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BookstoreDbContext>();
        await db.Database.MigrateAsync("20261022191522_InitialCreate");
        await db.Database.MigrateAsync();
        db.ChangeTracker.Clear();

        var preserved = await db.Books.Include(book => book.Author)
            .SingleAsync(book => book.BookId == created.BookId);
        Assert.Equal(created.Author.AuthorId, preserved.AuthorId);

        var next = new Bookstore.Api.Models.Author { Name = "Post Migration Author" };
        db.Authors.Add(next);
        await db.SaveChangesAsync();
        Assert.True(next.AuthorId > created.Author.AuthorId);
    }

    [Fact]
    public async Task Management_ui_requires_editor_role_and_forwards_crud_operations()
    {
        using var reader = CreateClient("search");
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.GetAsync("/api/ui/books/12")).StatusCode);

        using var editor = CreateClient("editor");
        var authors = await editor.GetFromJsonAsync<AuthorResponse[]>("/api/ui/authors");
        Assert.Contains(authors!, author => author.AuthorId == 77);

        var created = await editor.PostAsJsonAsync("/api/ui/books", NewBook(null, "UI Author", "UI Book"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(7001, (await created.Content.ReadFromJsonAsync<BookResponse>())?.BookId);

        Assert.Equal("UI Book", (await editor.GetFromJsonAsync<BookResponse>("/api/ui/books/12"))?.Title);
        Assert.Equal(HttpStatusCode.NoContent, (await editor.DeleteAsync("/api/ui/books/12")).StatusCode);
    }

    private HttpClient CreateClient(string mode)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Auth", mode);
        return client;
    }

    private static SaveBookRequest NewBook(int? authorId, string author, string title) =>
        new(new AuthorInput(authorId, author), title, null);
}
