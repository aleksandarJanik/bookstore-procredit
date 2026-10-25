using System.ComponentModel.DataAnnotations;
using Bookstore.Api.Models;

namespace Bookstore.Api.Contracts;

public sealed record AuthorInput(
    [Range(1, int.MaxValue)] int? AuthorId,
    [Required, StringLength(100, MinimumLength = 3)] string Name);

public sealed record SaveBookRequest(
    [Required] AuthorInput Author,
    [Required, StringLength(100, MinimumLength = 3)] string Title,
    string? SubTitle);

public sealed record AuthorResponse(int AuthorId, string Name);

public sealed record BookResponse(
    int BookId,
    AuthorResponse Author,
    string Title,
    string? SubTitle)
{
    public static BookResponse FromEntity(Book book) => new(
        book.BookId,
        new AuthorResponse(book.Author.AuthorId, book.Author.Name),
        book.Title,
        book.SubTitle);
}

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
