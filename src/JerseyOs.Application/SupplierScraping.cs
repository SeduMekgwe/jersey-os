using JerseyOs.Domain;

namespace JerseyOs.Application;

public sealed record SupplierScrapeRequest(
    Guid OrganizationId,
    Guid SupplierId,
    string StartUrl,
    string ProfileJson,
    string? Username,
    string? Password);

public sealed record SupplierScrapeResult(byte[] JsonUtf8, int ProductCount);

public interface ISupplierSiteCrawler
{
    Task<SupplierScrapeResult> CrawlAsync(SupplierScrapeRequest request, CancellationToken cancellationToken);
}
