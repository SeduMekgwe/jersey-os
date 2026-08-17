using Asp.Versioning;
using JerseyOs.Application;
using JerseyOs.Contracts;
using JerseyOs.Infrastructure;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JerseyOs.Api;

[ApiController]
[ApiVersion(1.0)]
[Authorize(Policy = Permissions.AuditRead)]
[Route("api/v{version:apiVersion}/audit")]
public sealed class AuditController(ISender sender) : ControllerBase
{
    [HttpGet("events")]
    [ProducesResponseType<IReadOnlyCollection<AuditEventResponse>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyCollection<AuditEventResponse>> ListEvents(
        [FromQuery] string? action,
        [FromQuery] string? entityType,
        CancellationToken cancellationToken) =>
        sender.Send(new ListAuditEventsQuery(action, entityType), cancellationToken);
}

[ApiController]
[ApiVersion(1.0)]
[Authorize(Policy = Permissions.NotificationsRead)]
[Route("api/v{version:apiVersion}/notifications")]
public sealed class NotificationsController(ISender sender) : ControllerBase
{
    [HttpGet("templates")]
    [ProducesResponseType<IReadOnlyCollection<NotificationTemplateResponse>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyCollection<NotificationTemplateResponse>> ListTemplates(CancellationToken cancellationToken) =>
        sender.Send(new ListNotificationTemplatesQuery(), cancellationToken);

    [HttpGet("messages")]
    [ProducesResponseType<IReadOnlyCollection<NotificationMessageResponse>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyCollection<NotificationMessageResponse>> ListMessages(CancellationToken cancellationToken) =>
        sender.Send(new ListNotificationMessagesQuery(), cancellationToken);
}

[ApiController]
[ApiVersion(1.0)]
[Authorize(Policy = Permissions.IntegrationsManage)]
[Route("api/v{version:apiVersion}/integrations/api-keys")]
public sealed class ApiKeysController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyCollection<ApiKeyResponse>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyCollection<ApiKeyResponse>> List(CancellationToken cancellationToken) =>
        sender.Send(new ListApiKeysQuery(), cancellationToken);

    [HttpPost]
    [ProducesResponseType<CreatedApiKeyResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<CreatedApiKeyResponse>> Create(
        CreateApiKeyRequest request, CancellationToken cancellationToken)
    {
        var created = await sender.Send(new CreateApiKeyCommand(request.Name, request.Scopes), cancellationToken);
        return Created($"/api/v1/integrations/api-keys/{created.Id}", created);
    }

    [HttpPost("{apiKeyId:guid}/revoke")]
    [ProducesResponseType<ApiKeyResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiKeyResponse>> Revoke(Guid apiKeyId, CancellationToken cancellationToken)
    {
        var revoked = await sender.Send(new RevokeApiKeyCommand(apiKeyId), cancellationToken);
        return revoked is null ? NotFound() : Ok(revoked);
    }
}
