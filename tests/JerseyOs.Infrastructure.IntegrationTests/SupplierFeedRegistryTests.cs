using JerseyOs.Application;
using JerseyOs.Domain;
using JerseyOs.Infrastructure;

namespace JerseyOs.Infrastructure.IntegrationTests;

public sealed class SupplierFeedRegistryTests
{
    [Fact]
    public void RegistryResolvesCsvAndJson()
    {
        var registry = new SupplierCatalogFeedRegistry(
        [
            new CsvSupplierCatalogFeed(),
            new JsonSupplierCatalogFeed()
        ]);
        Assert.Equal(SupplierFeedFormats.Csv, registry.Resolve("csv").Format);
        Assert.Equal(SupplierFeedFormats.Json, registry.Resolve("JSON").Format);
        Assert.Throws<InvalidOperationException>(() => registry.Resolve("xml"));
    }

    [Fact]
    public void ParseImportBatchJob_ResolveFormat_PrefersSupplierThenFileName()
    {
        Assert.Equal(
            SupplierFeedFormats.Json,
            ParseImportBatchJob.ResolveFormat("text/csv", "file.bin", "imports/x.bin", SupplierFeedFormats.Json));
        Assert.Equal(
            SupplierFeedFormats.Json,
            ParseImportBatchJob.ResolveFormat("text/csv", "a.json", "imports/a.json", null));
        Assert.Equal(
            SupplierFeedFormats.Csv,
            ParseImportBatchJob.ResolveFormat("text/csv", "a.csv", "imports/a.csv", null));
    }
}
