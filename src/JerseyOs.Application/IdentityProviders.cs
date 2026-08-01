namespace JerseyOs.Application;

/// <summary>
/// Future OAuth/OIDC provider boundary. Password login remains the active foundation path.
/// </summary>
public interface IExternalIdentityProvider
{
    string ProviderName { get; }
    Task<IssuedAuth?> AuthenticateAsync(string authorizationCode, string? redirectUri, CancellationToken cancellationToken);
}

/// <summary>
/// Future passwordless (magic-link / OTP) boundary. Not used by the password login flow.
/// </summary>
public interface IPasswordlessIdentityProvider
{
    Task RequestSignInTokenAsync(string email, CancellationToken cancellationToken);
    Task<IssuedAuth?> CompleteSignInAsync(string oneTimeToken, CancellationToken cancellationToken);
}

public sealed class UnsupportedExternalIdentityProvider : IExternalIdentityProvider
{
    public string ProviderName => "none";

    public Task<IssuedAuth?> AuthenticateAsync(string authorizationCode, string? redirectUri, CancellationToken cancellationToken) =>
        throw new NotSupportedException("External identity providers are not configured for this deployment.");
}

public sealed class UnsupportedPasswordlessIdentityProvider : IPasswordlessIdentityProvider
{
    public Task RequestSignInTokenAsync(string email, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Passwordless sign-in is not configured for this deployment.");

    public Task<IssuedAuth?> CompleteSignInAsync(string oneTimeToken, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Passwordless sign-in is not configured for this deployment.");
}
