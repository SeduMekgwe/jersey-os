using JerseyOs.Application;
using JerseyOs.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace JerseyOs.Infrastructure.IntegrationTests;

public sealed class ObjectStorageTests
{
    [Fact]
    public async Task LocalObjectStorage_PutDeleteUrl_AndOpenRead()
    {
        var root = Path.Combine(Path.GetTempPath(), "jerseyos-local-storage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var storage = new LocalObjectStorage(
                Options.Create(new ObjectStorageOptions
                {
                    LocalRootPath = root,
                    PublicBasePath = "/media"
                }),
                new TestHostEnvironment(root));

            await using (var content = new MemoryStream("hello"u8.ToArray()))
            {
                var key = await storage.PutAsync("imports/a.csv", content, "text/csv", CancellationToken.None);
                Assert.Equal("imports/a.csv", key);
            }

            Assert.Equal("/media/imports/a.csv", storage.GetUrl("imports/a.csv"));

            await using (var read = await storage.OpenReadAsync("imports/a.csv", CancellationToken.None))
            using (var reader = new StreamReader(read))
            {
                Assert.Equal("hello", await reader.ReadToEndAsync());
            }

            await storage.DeleteAsync("imports/a.csv", CancellationToken.None);
            await Assert.ThrowsAsync<FileNotFoundException>(() =>
                storage.OpenReadAsync("imports/a.csv", CancellationToken.None));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void LocalObjectStorage_GetUrl_UsesPublicBaseUrlWhenSet()
    {
        var root = Path.Combine(Path.GetTempPath(), "jerseyos-local-url-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var storage = new LocalObjectStorage(
                Options.Create(new ObjectStorageOptions
                {
                    LocalRootPath = root,
                    PublicBasePath = "/media",
                    PublicBaseUrl = "https://api.example.com/media"
                }),
                new TestHostEnvironment(root));

            Assert.Equal("https://api.example.com/media/products/x.png", storage.GetUrl("products/x.png"));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task AzureBlobObjectStorage_PutDeleteUrl_AndOpenRead_ViaGateway()
    {
        var gateway = new FakeAzureBlobGateway(new Uri("https://acct.blob.core.windows.net/media"));
        var storage = new AzureBlobObjectStorage(
            Options.Create(new ObjectStorageOptions
            {
                Provider = "AzureBlob",
                AzureBlob = new AzureBlobObjectStorageOptions
                {
                    ConnectionString = "UseDevelopmentStorage=true",
                    ContainerName = "media",
                    PublicBaseUrl = "https://cdn.example.com/media"
                }
            }),
            gateway);

        await using (var content = new MemoryStream("blob"u8.ToArray()))
        {
            var key = await storage.PutAsync("products/1.png", content, "image/png", CancellationToken.None);
            Assert.Equal("products/1.png", key);
        }

        Assert.Equal("https://cdn.example.com/media/products/1.png", storage.GetUrl("products/1.png"));
        Assert.Equal("image/png", gateway.Blobs["products/1.png"].ContentType);

        await using (var read = await storage.OpenReadAsync("products/1.png", CancellationToken.None))
        using (var reader = new StreamReader(read))
        {
            Assert.Equal("blob", await reader.ReadToEndAsync());
        }

        await storage.DeleteAsync("products/1.png", CancellationToken.None);
        Assert.False(gateway.Blobs.ContainsKey("products/1.png"));
    }

    [Fact]
    public void AzureBlobObjectStorage_GetUrl_FallsBackToContainerUri()
    {
        var gateway = new FakeAzureBlobGateway(new Uri("https://acct.blob.core.windows.net/media"));
        var storage = new AzureBlobObjectStorage(
            Options.Create(new ObjectStorageOptions
            {
                Provider = "AzureBlob",
                AzureBlob = new AzureBlobObjectStorageOptions
                {
                    ConnectionString = "UseDevelopmentStorage=true",
                    ContainerName = "media"
                }
            }),
            gateway);

        Assert.Equal(
            "https://acct.blob.core.windows.net/media/products/1.png",
            storage.GetUrl("products/1.png"));
    }

    [Fact]
    public void AddObjectStorage_AzureBlob_WithoutSecrets_Throws()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ObjectStorage:Provider"] = "AzureBlob",
                ["ObjectStorage:AzureBlob:ContainerName"] = "media"
            })
            .Build();

        var ex = Assert.Throws<InvalidOperationException>(() => services.AddObjectStorage(config));
        Assert.Contains("ConnectionString", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddObjectStorage_Local_RegistersLocalAdapter()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment(Path.GetTempPath()));
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ObjectStorage:Provider"] = "Local",
                ["ObjectStorage:LocalRootPath"] = "App_Data/object-storage",
                ["ObjectStorage:PublicBasePath"] = "/media"
            })
            .Build();

        services.AddObjectStorage(config);
        using var provider = services.BuildServiceProvider();
        Assert.IsType<LocalObjectStorage>(provider.GetRequiredService<IObjectStorage>());
    }

    private sealed class FakeAzureBlobGateway(Uri containerUri) : IAzureBlobGateway
    {
        public Dictionary<string, (byte[] Data, string ContentType)> Blobs { get; } = new(StringComparer.Ordinal);
        public Uri ContainerUri { get; } = containerUri;

        public async Task UploadAsync(
            string key, Stream content, string contentType, CancellationToken cancellationToken)
        {
            using var ms = new MemoryStream();
            await content.CopyToAsync(ms, cancellationToken);
            Blobs[key] = (ms.ToArray(), contentType);
        }

        public Task DeleteAsync(string key, CancellationToken cancellationToken)
        {
            Blobs.Remove(key);
            return Task.CompletedTask;
        }

        public Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken)
        {
            if (!Blobs.TryGetValue(key, out var blob))
            {
                throw new FileNotFoundException("missing", key);
            }

            Stream stream = new MemoryStream(blob.Data);
            return Task.FromResult(stream);
        }
    }

    private sealed class TestHostEnvironment(string root) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "tests";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
