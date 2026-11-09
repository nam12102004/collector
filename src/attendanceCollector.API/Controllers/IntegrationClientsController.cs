using attendanceCollector.API.Extensions;
using attendanceCollector.Application.Contracts;
using attendanceCollector.Application.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace attendanceCollector.API.Controllers;

[ApiController]
[Route("api/v1/integration-clients")]
[Authorize(Policy = "BootstrapAdmin", AuthenticationSchemes = AuthenticationExtensions.CookieScheme + "," + JwtBearerDefaults.AuthenticationScheme)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class IntegrationClientsController(IIntegrationClientService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default) =>
        Ok(await service.GetClientsAsync(page, pageSize, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) => Ok(await service.GetClientAsync(id, cancellationToken));

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(65536)]
    public async Task<IActionResult> Create([FromBody] CreateIntegrationClientRequest request, CancellationToken cancellationToken) =>
        Ok(await service.CreateAsync(request, cancellationToken));

    [HttpPut("{id:guid}/device-grants")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(65536)]
    public async Task<IActionResult> ReplaceGrants(Guid id, [FromBody] ReplaceIntegrationGrantsRequest request, CancellationToken cancellationToken) =>
        Ok(await service.ReplaceGrantsAsync(id, request, cancellationToken));

    [HttpPatch("{id:guid}/active")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetActive(Guid id, [FromBody] SetIntegrationActiveRequest request, CancellationToken cancellationToken) =>
        Ok(await service.SetActiveAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/keys")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateKey(Guid id, [FromBody] CreateIntegrationKeyRequest request, CancellationToken cancellationToken) =>
        Ok(await service.CreateKeyAsync(id, request, cancellationToken));

    [HttpDelete("{id:guid}/keys/{keyId:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RevokeKey(Guid id, Guid keyId, CancellationToken cancellationToken)
    {
        await service.RevokeKeyAsync(id, keyId, cancellationToken);
        return Ok();
    }
}
