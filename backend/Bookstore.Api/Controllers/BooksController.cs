using System.ComponentModel.DataAnnotations;
using Bookstore.Api.Contracts;
using Bookstore.Api.Data;
using Bookstore.Api.Models;
using Bookstore.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Bookstore.Api.Controllers;

[ApiController]
[Route("api/books")]
public sealed class BooksController(BookstoreDbContext db) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = Policies.ManageBooks)]
    [ProducesResponseType<BookResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<BookResponse>> Create(
        SaveBookRequest request,
        CancellationToken cancellationToken)
    {
        if (ValidateTrimmedText(request, out var validationResult))
        {
            return validationResult;
        }

        var (author, authorError) = await ResolveAuthorAsync(request.Author, cancellationToken);
        if (authorError is not null)
        {
            return authorError;
        }

        var book = new Book
        {
            Author = author!,
            Title = request.Title.Trim(),
            SubTitle = NullIfWhiteSpace(request.SubTitle)
        };
        db.Books.Add(book);

        await db.SaveChangesAsync(cancellationToken);

        var response = BookResponse.FromEntity(book);
        return CreatedAtAction(nameof(GetById), new { bookId = book.BookId }, response);
    }

    [HttpGet("{bookId:int}")]
    [Authorize(Policy = Policies.ManageBooks)]
    [ProducesResponseType<BookResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookResponse>> GetById(int bookId, CancellationToken cancellationToken)
    {
        var book = await db.Books.AsNoTracking()
            .Include(x => x.Author)
            .SingleOrDefaultAsync(x => x.BookId == bookId, cancellationToken);
        return book is null ? NotFound() : Ok(BookResponse.FromEntity(book));
    }

    [HttpPut("{bookId:int}")]
    [Authorize(Policy = Policies.ManageBooks)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        int bookId,
        SaveBookRequest request,
        CancellationToken cancellationToken)
    {
        if (ValidateTrimmedText(request, out var validationResult))
        {
            return validationResult;
        }

        var book = await db.Books.SingleOrDefaultAsync(x => x.BookId == bookId, cancellationToken);
        if (book is null)
        {
            return NotFound();
        }

        var (author, authorError) = await ResolveAuthorAsync(request.Author, cancellationToken);
        if (authorError is not null)
        {
            return authorError;
        }

        book.Author = author!;
        book.Title = request.Title.Trim();
        book.SubTitle = NullIfWhiteSpace(request.SubTitle);

        await db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpDelete("{bookId:int}")]
    [Authorize(Policy = Policies.ManageBooks)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int bookId, CancellationToken cancellationToken)
    {
        var book = await db.Books.SingleOrDefaultAsync(x => x.BookId == bookId, cancellationToken);
        if (book is null)
        {
            return NotFound();
        }

        db.Books.Remove(book);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("search")]
    [Authorize(Policy = Policies.SearchBooks)]
    [ProducesResponseType<PagedResult<BookResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<BookResponse>>> Search(
        [FromQuery] string? title,
        [FromQuery] string? author,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = db.Books.AsNoTracking().Include(x => x.Author).AsQueryable();

        if (!string.IsNullOrWhiteSpace(title))
        {
            var pattern = $"%{EscapeLike(title.Trim())}%";
            query = query.Where(x => EF.Functions.Like(x.Title, pattern, @"\"));
        }

        if (!string.IsNullOrWhiteSpace(author))
        {
            var pattern = $"%{EscapeLike(author.Trim())}%";
            query = query.Where(x => EF.Functions.Like(x.Author.Name, pattern, @"\"));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var books = await query.OrderBy(x => x.BookId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new BookResponse(
                x.BookId,
                new AuthorResponse(x.Author.AuthorId, x.Author.Name),
                x.Title,
                x.SubTitle))
            .ToListAsync(cancellationToken);

        return Ok(new PagedResult<BookResponse>(
            books,
            page,
            pageSize,
            totalCount,
            (int)Math.Ceiling(totalCount / (double)pageSize)));
    }

    private async Task<(Author? Author, ObjectResult? Error)> ResolveAuthorAsync(
        AuthorInput input,
        CancellationToken cancellationToken)
    {
        if (input.AuthorId is null)
        {
            var author = new Author { Name = input.Name.Trim() };
            db.Authors.Add(author);
            return (author, null);
        }

        var existingAuthor = await db.Authors.FindAsync([input.AuthorId.Value], cancellationToken);
        if (existingAuthor is null)
        {
            var errors = new Dictionary<string, string[]>
            {
                ["author.authorId"] = [$"Author {input.AuthorId.Value} does not exist."]
            };
            return (null, new BadRequestObjectResult(new ValidationProblemDetails(errors)));
        }

        return string.Equals(existingAuthor.Name, input.Name.Trim(), StringComparison.OrdinalIgnoreCase)
            ? (existingAuthor, null)
            : (null, ConflictProblem(input.AuthorId.Value));
    }

    private bool ValidateTrimmedText(SaveBookRequest request, out ObjectResult result)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.Title.Trim().Length < 3)
        {
            errors["title"] = ["The title must contain at least 3 non-whitespace characters."];
        }
        if (request.Author.Name.Trim().Length < 3)
        {
            errors["author.name"] = ["The author name must contain at least 3 non-whitespace characters."];
        }

        result = new BadRequestObjectResult(new ValidationProblemDetails(errors));
        return errors.Count > 0;
    }

    private ObjectResult ConflictProblem(int authorId) => Problem(
        statusCode: StatusCodes.Status409Conflict,
        title: "Author conflict",
        detail: $"Author {authorId} already exists with a different name.");

    private static string EscapeLike(string value) => value
        .Replace(@"\", @"\\", StringComparison.Ordinal)
        .Replace("%", @"\%", StringComparison.Ordinal)
        .Replace("_", @"\_", StringComparison.Ordinal)
        .Replace("[", @"\[", StringComparison.Ordinal);

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
