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
[Authorize(Policy = Permissions.ImportRead)]
[Route("api/v{version:apiVersion}/import")]
public sealed class ImportController(ISender sender) : ControllerBase
{
    [HttpGet("suppliers")]
    [ProducesResponseType<IReadOnlyCollection<SupplierResponse>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyCollection<SupplierResponse>> ListSuppliers(CancellationToken cancellationToken) =>
        sender.Send(new ListSuppliersQuery(), cancellationToken);

    [HttpPost("suppliers")]
    [Authorize(Policy = Permissions.ImportUpload)]
    [ProducesResponseType<SupplierResponse>(StatusCodes.Status200OK)]
    public Task<SupplierResponse> CreateSupplier(CreateSupplierRequest request, CancellationToken cancellationToken) =>
        sender.Send(
            new CreateSupplierCommand(
                request.Name,
                request.Code,
                request.FeedKind,
                request.FeedFormat,
                request.FeedUrl,
                request.FeedBearerToken,
                request.ScrapeProfileJson,
                request.ScrapeUsername,
                request.ScrapePassword,
                request.SyncCron),
            cancellationToken);

    [HttpPut("suppliers/{supplierId:guid}/feed")]
    [Authorize(Policy = Permissions.ImportUpload)]
    [ProducesResponseType<SupplierResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SupplierResponse>> UpdateSupplierFeed(
        Guid supplierId, UpdateSupplierFeedRequest request, CancellationToken cancellationToken)
    {
        var supplier = await sender.Send(
            new UpdateSupplierFeedCommand(
                supplierId,
                request.FeedKind,
                request.FeedFormat,
                request.FeedUrl,
                request.FeedBearerToken,
                request.ScrapeProfileJson,
                request.ScrapeUsername,
                request.ScrapePassword,
                request.SyncCron),
            cancellationToken);
        return supplier is null ? NotFound() : Ok(supplier);
    }

    [HttpPost("suppliers/{supplierId:guid}/sync")]
    [Authorize(Policy = Permissions.ImportUpload)]
    [ProducesResponseType<SupplierResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SupplierResponse>> SyncSupplierFeed(
        Guid supplierId, CancellationToken cancellationToken)
    {
        var supplier = await sender.Send(new SyncSupplierFeedCommand(supplierId), cancellationToken);
        return supplier is null ? NotFound() : Ok(supplier);
    }

    [HttpGet("suppliers/{supplierId:guid}/scrape-runs")]
    [ProducesResponseType<IReadOnlyCollection<SupplierScrapeRunResponse>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyCollection<SupplierScrapeRunResponse>> ListScrapeRuns(
        Guid supplierId, CancellationToken cancellationToken) =>
        sender.Send(new ListSupplierScrapeRunsQuery(supplierId), cancellationToken);

    [HttpGet("batches")]
    [ProducesResponseType<IReadOnlyCollection<ImportBatchSummaryResponse>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyCollection<ImportBatchSummaryResponse>> ListBatches(CancellationToken cancellationToken) =>
        sender.Send(new ListImportBatchesQuery(), cancellationToken);

    [HttpGet("batches/{batchId:guid}")]
    [ProducesResponseType<ImportBatchDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ImportBatchDetailResponse>> GetBatch(Guid batchId, CancellationToken cancellationToken)
    {
        var batch = await sender.Send(new GetImportBatchQuery(batchId), cancellationToken);
        return batch is null ? NotFound() : Ok(batch);
    }

    [HttpPost("batches")]
    [Authorize(Policy = Permissions.ImportUpload)]
    [RequestSizeLimit(1_048_576)]
    [ProducesResponseType<ImportBatchSummaryResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ImportBatchSummaryResponse>> UploadBatch(
        [FromForm] Guid supplierId,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file.Length <= 0)
        {
            return BadRequest();
        }

        await using var stream = file.OpenReadStream();
        var batch = await sender.Send(
            new UploadImportBatchCommand(supplierId, stream, file.FileName, file.ContentType),
            cancellationToken);
        return CreatedAtAction(nameof(GetBatch), new { batchId = batch.Id, version = "1.0" }, batch);
    }

    [HttpPut("items/{itemId:guid}")]
    [Authorize(Policy = Permissions.ImportReview)]
    [ProducesResponseType<ImportItemResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ImportItemResponse>> UpdateItem(
        Guid itemId, UpdateImportItemRequest request, CancellationToken cancellationToken)
    {
        var item = await sender.Send(
            new UpdateImportItemCommand(
                itemId,
                request.Name,
                request.Slug,
                request.Sku,
                request.Size,
                request.StyleCode,
                request.TeamName,
                request.SeasonName,
                request.Quantity,
                request.ImageUrl),
            cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost("items/{itemId:guid}/approve")]
    [Authorize(Policy = Permissions.ImportReview)]
    [ProducesResponseType<ImportItemResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ImportItemResponse>> ApproveItem(
        Guid itemId, [FromBody] ReviewImportItemRequest? request, CancellationToken cancellationToken)
    {
        var item = await sender.Send(new ApproveImportItemCommand(itemId, request?.Note), cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost("items/{itemId:guid}/reject")]
    [Authorize(Policy = Permissions.ImportReview)]
    [ProducesResponseType<ImportItemResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ImportItemResponse>> RejectItem(
        Guid itemId, [FromBody] ReviewImportItemRequest? request, CancellationToken cancellationToken)
    {
        var item = await sender.Send(new RejectImportItemCommand(itemId, request?.Note), cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost("items/bulk-approve")]
    [Authorize(Policy = Permissions.ImportReview)]
    [ProducesResponseType<IReadOnlyCollection<ImportItemResponse>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyCollection<ImportItemResponse>> BulkApprove(
        BulkApproveImportItemsRequest request, CancellationToken cancellationToken) =>
        sender.Send(new BulkApproveImportItemsCommand(request.ItemIds, request.Note), cancellationToken);
}
