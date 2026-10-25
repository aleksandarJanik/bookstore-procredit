using Bookstore.Api.Contracts;
using Bookstore.Api.Data;
using Bookstore.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Bookstore.Api.Controllers;

[ApiController]
[Route("api/authors")]
[Authorize(Policy = Policies.ManageBooks)]
public sealed class AuthorsController(BookstoreDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AuthorResponse>>> Get(CancellationToken cancellationToken) =>
        Ok(await db.Authors.AsNoTracking()
            .OrderBy(x => x.Name)
            .ThenBy(x => x.AuthorId)
            .Select(x => new AuthorResponse(x.AuthorId, x.Name))
            .ToListAsync(cancellationToken));
}
