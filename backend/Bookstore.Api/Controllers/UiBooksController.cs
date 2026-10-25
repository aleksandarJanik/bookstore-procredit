using Bookstore.Api.Contracts;
using Bookstore.Api.Security;
using Bookstore.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bookstore.Api.Controllers;

[ApiController]
[Route("api/ui/books")]
[Authorize(Policy = Policies.UseManagementUi)]
public sealed class UiBooksController(IBookstoreManagementClient managementClient) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(SaveBookRequest request, CancellationToken cancellationToken) =>
        await ForwardAsync(await managementClient.CreateAsync(request, cancellationToken), cancellationToken);

    [HttpGet("{bookId:int}")]
    public async Task<IActionResult> Get(int bookId, CancellationToken cancellationToken) =>
        await ForwardAsync(await managementClient.GetAsync(bookId, cancellationToken), cancellationToken);

    [HttpDelete("{bookId:int}")]
    public async Task<IActionResult> Delete(int bookId, CancellationToken cancellationToken) =>
        await ForwardAsync(await managementClient.DeleteAsync(bookId, cancellationToken), cancellationToken);

    private static async Task<ContentResult> ForwardAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        using (response)
        {
            return new ContentResult
            {
                StatusCode = (int)response.StatusCode,
                ContentType = response.Content.Headers.ContentType?.ToString(),
                Content = await response.Content.ReadAsStringAsync(cancellationToken)
            };
        }
    }
}
