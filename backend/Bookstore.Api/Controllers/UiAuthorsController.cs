using Bookstore.Api.Security;
using Bookstore.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bookstore.Api.Controllers;

[ApiController]
[Route("api/ui/authors")]
[Authorize(Policy = Policies.UseManagementUi)]
public sealed class UiAuthorsController(IBookstoreManagementClient managementClient) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        using var response = await managementClient.GetAuthorsAsync(cancellationToken);
        return new ContentResult
        {
            StatusCode = (int)response.StatusCode,
            ContentType = response.Content.Headers.ContentType?.ToString(),
            Content = await response.Content.ReadAsStringAsync(cancellationToken)
        };
    }
}
