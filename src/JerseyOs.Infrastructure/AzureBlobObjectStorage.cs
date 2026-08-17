using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using JerseyOs.Application;
using Microsoft.Extensions.Options;

namespace JerseyOs.Infrastructure;

public sealed class AzureBlobObjectStorageOptions
{
    /// <summary>ConnectionString (default) or ManagedIdentity.</summary>
    public string AuthMode { get; set; } = "ConnectionString";
    public string ConnectionString { get; set; } = string.Empty;
    /// <summary>Blob service URI when AuthMode=ManagedIdentity (e.g. https://account.blob.core.windows.net).</summary>
    public string? ServiceUri { get; set; }
    public string ContainerName { get; set; } = string.Empty;
    public string? PublicBaseUrl { get; set; }
}

/// <summary>
/// Thin gateway over BlobContainerClient so unit tests can substitute uploads/downloads.
/// </summary>
public interface IAzureBlobGateway
{
    Task UploadAsync(string key, Stream content, string contentType, CancellationToken cancellationToken);
    Task DeleteAsync(string key, CancellationToken cancellationToken);
    Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken);
    Uri ContainerUri { get; }
}

public sealed class AzureBlobContainerGateway(BlobContainerClient container) : IAzureBlobGateway
{
    public Uri ContainerUri => container.Uri;

    public async Task UploadAsync(
        string key, Stream content, string contentType, CancellationToken cancellationToken)
    {
        var blob = container.GetBlobClient(NormalizeKey(key));
        await blob.UploadAsync(
                content,
                new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken) =>
        await container.GetBlobClient(NormalizeKey(key))
            .DeleteIfExistsAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

    public async Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken)
    {
        var blob = container.GetBlobClient(NormalizeKey(key));
        if (!await blob.ExistsAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new FileNotFoundException("Object was not found in Azure Blob storage.", key);
        }

        var stream = new MemoryStream();
        await blob.DownloadToAsync(stream, cancellationToken).ConfigureAwait(false);
        stream.Position = 0;
        return stream;
    }

    private static string NormalizeKey(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var normalized = key.Replace('\\', '/').TrimStart('/');
        if (normalized.Contains("..", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Invalid object storage key.");
        }

        return normalized;
    }
}

public sealed class AzureBlobObjectStorage(
    IOptions<ObjectStorageOptions> options,
    IAzureBlobGateway gateway) : IObjectStorage
{
    public async Task<string> PutAsync(
        string key, Stream content, string contentType, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        var normalized = NormalizeKey(key);
        await gateway.UploadAsync(normalized, content, contentType, cancellationToken).ConfigureAwait(false);
        return normalized;
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken) =>
        gateway.DeleteAsync(NormalizeKey(key), cancellationToken);

    public string GetUrl(string key)
    {
        var normalized = NormalizeKey(key);
        var baseUrl = ResolvePublicBaseUrl().TrimEnd('/');
        return $"{baseUrl}/{normalized}";
    }

    public Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken) =>
        gateway.OpenReadAsync(NormalizeKey(key), cancellationToken);

    internal string ResolvePublicBaseUrl()
    {
        var opts = options.Value;
        if (!string.IsNullOrWhiteSpace(opts.AzureBlob.PublicBaseUrl))
        {
            return opts.AzureBlob.PublicBaseUrl.TrimEnd('/');
        }

        if (!string.IsNullOrWhiteSpace(opts.PublicBaseUrl))
        {
            return opts.PublicBaseUrl.TrimEnd('/');
        }

        return gateway.ContainerUri.ToString().TrimEnd('/');
    }

    private static string NormalizeKey(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var normalized = key.Replace('\\', '/').TrimStart('/');
        if (normalized.Contains("..", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Invalid object storage key.");
        }

        return normalized;
    }
}

public static class ObjectStorageRegistration
{
    public static void ValidatePublicBaseUrl(string? value, string settingName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            throw new InvalidOperationException($"{settingName} must be an absolute http(s) URL.");
        }
    }

    public static bool IsManagedIdentity(string? authMode) =>
        string.Equals(authMode, "ManagedIdentity", StringComparison.OrdinalIgnoreCase);

    public static void ValidateAzureBlobOptions(ObjectStorageOptions options)
    {
        ValidatePublicBaseUrl(options.PublicBaseUrl, "ObjectStorage:PublicBaseUrl");
        ValidatePublicBaseUrl(options.AzureBlob.PublicBaseUrl, "ObjectStorage:AzureBlob:PublicBaseUrl");

        if (string.IsNullOrWhiteSpace(options.AzureBlob.ContainerName))
        {
            throw new InvalidOperationException(
                "ObjectStorage:AzureBlob:ContainerName is required when Provider is AzureBlob.");
        }

        if (IsManagedIdentity(options.AzureBlob.AuthMode))
        {
            if (string.IsNullOrWhiteSpace(options.AzureBlob.ServiceUri)
                || !Uri.TryCreate(options.AzureBlob.ServiceUri.Trim(), UriKind.Absolute, out var serviceUri)
                || serviceUri.Scheme != Uri.UriSchemeHttps)
            {
                throw new InvalidOperationException(
                    "ObjectStorage:AzureBlob:ServiceUri must be an absolute https URI when AuthMode is ManagedIdentity.");
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(options.AzureBlob.ConnectionString))
        {
            throw new InvalidOperationException(
                "ObjectStorage:AzureBlob:ConnectionString is required when Provider is AzureBlob and AuthMode is ConnectionString.");
        }
    }

    public static bool IsAzureBlobProvider(string? provider) =>
        string.Equals(provider, "AzureBlob", StringComparison.OrdinalIgnoreCase);
}
