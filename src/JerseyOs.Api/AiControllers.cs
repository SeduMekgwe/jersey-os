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
[Authorize(Policy = Permissions.AiRead)]
[Route("api/v{version:apiVersion}/ai")]
public sealed class AiController(ISender sender) : ControllerBase
{
    [HttpGet("templates")]
    [ProducesResponseType<IReadOnlyCollection<AiPromptTemplateResponse>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyCollection<AiPromptTemplateResponse>> ListTemplates(CancellationToken cancellationToken) =>
        sender.Send(new ListAiPromptTemplatesQuery(), cancellationToken);

    [HttpGet("generations")]
    [ProducesResponseType<IReadOnlyCollection<AiGenerationResponse>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyCollection<AiGenerationResponse>> ListGenerations(
        [FromQuery] string? targetType,
        [FromQuery] Guid? targetId,
        CancellationToken cancellationToken) =>
        sender.Send(new ListAiGenerationsQuery(targetType, targetId), cancellationToken);

    [HttpPost("generations")]
    [Authorize(Policy = Permissions.AiWrite)]
    [ProducesResponseType<AiGenerationResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<AiGenerationResponse>> CreateGeneration(
        CreateAiGenerationRequest request, CancellationToken cancellationToken)
    {
        var generation = await sender.Send(
            new EnqueueAiGenerationCommand(request.Kind, request.TargetType, request.TargetId, request.PromptTemplateId),
            cancellationToken);
        return CreatedAtAction(nameof(ListGenerations), new { version = "1.0", targetType = generation.TargetType, targetId = generation.TargetId }, generation);
    }

    [HttpPost("generations/{generationId:guid}/approve")]
    [Authorize(Policy = Permissions.AiWrite)]
    [ProducesResponseType<AiGenerationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AiGenerationResponse>> Approve(
        Guid generationId, CancellationToken cancellationToken)
    {
        var generation = await sender.Send(new ApproveAiGenerationCommand(generationId), cancellationToken);
        return generation is null ? NotFound() : Ok(generation);
    }

    [HttpPost("generations/{generationId:guid}/reject")]
    [Authorize(Policy = Permissions.AiWrite)]
    [ProducesResponseType<AiGenerationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AiGenerationResponse>> Reject(
        Guid generationId, RejectAiGenerationRequest? request, CancellationToken cancellationToken)
    {
        var generation = await sender.Send(new RejectAiGenerationCommand(generationId, request?.Note), cancellationToken);
        return generation is null ? NotFound() : Ok(generation);
    }
}
