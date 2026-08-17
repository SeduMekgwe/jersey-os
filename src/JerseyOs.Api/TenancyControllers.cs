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
[Authorize(Policy = Permissions.PlatformAdmin)]
[Route("api/v{version:apiVersion}/admin/organizations")]
public sealed class AdminOrganizationsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyCollection<OrganizationSummaryResponse>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyCollection<OrganizationSummaryResponse>> List(CancellationToken cancellationToken) =>
        sender.Send(new ListOrganizationsQuery(), cancellationToken);

    [HttpPost]
    [ProducesResponseType<OrganizationSummaryResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<OrganizationSummaryResponse>> Provision(
        ProvisionOrganizationRequest request, CancellationToken cancellationToken)
    {
        var created = await sender.Send(
            new ProvisionOrganizationCommand(
                request.Name,
                request.Slug,
                request.AdminEmail,
                request.AdminPassword,
                request.DefaultCurrency),
            cancellationToken);
        return Created($"/api/v1/admin/organizations/{created.Id}", created);
    }

    [HttpGet("{organizationId:guid}/quotas")]
    [ProducesResponseType<OrganizationQuotaResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrganizationQuotaResponse>> GetQuota(
        Guid organizationId, CancellationToken cancellationToken)
    {
        var quota = await sender.Send(new GetOrganizationQuotaQuery(organizationId), cancellationToken);
        return quota is null ? NotFound() : Ok(quota);
    }

    [HttpPut("{organizationId:guid}/quotas")]
    [ProducesResponseType<OrganizationQuotaResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrganizationQuotaResponse>> UpdateQuota(
        Guid organizationId, UpdateOrganizationQuotaRequest request, CancellationToken cancellationToken)
    {
        var quota = await sender.Send(
            new UpdateOrganizationQuotaCommand(
                organizationId,
                request.MaxProducts,
                request.MaxMembers,
                request.MaxImportBatchesPerDay,
                request.MaxAiGenerationsPerDay),
            cancellationToken);
        return quota is null ? NotFound() : Ok(quota);
    }
}

[ApiController]
[ApiVersion(1.0)]
[Authorize(Policy = Permissions.OrgMembersManage)]
[Route("api/v{version:apiVersion}/org/invitations")]
public sealed class OrganizationInvitationsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyCollection<OrganizationInvitationResponse>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyCollection<OrganizationInvitationResponse>> List(CancellationToken cancellationToken) =>
        sender.Send(new ListOrganizationInvitationsQuery(), cancellationToken);

    [HttpPost]
    [ProducesResponseType<CreatedOrganizationInvitationResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<CreatedOrganizationInvitationResponse>> Create(
        CreateOrganizationInvitationRequest request, CancellationToken cancellationToken)
    {
        var created = await sender.Send(
            new CreateOrganizationInvitationCommand(request.Email, request.Role), cancellationToken);
        return Created($"/api/v1/org/invitations/{created.Id}", created);
    }

    [HttpPost("{invitationId:guid}/revoke")]
    [ProducesResponseType<OrganizationInvitationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrganizationInvitationResponse>> Revoke(
        Guid invitationId, CancellationToken cancellationToken)
    {
        var revoked = await sender.Send(new RevokeOrganizationInvitationCommand(invitationId), cancellationToken);
        return revoked is null ? NotFound() : Ok(revoked);
    }
}
