namespace JerseyOs.Application;

public interface IObjectStorage
{
    Task<string> PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken);
    Task DeleteAsync(string key, CancellationToken cancellationToken);
    string GetUrl(string key);
    Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken);
}
