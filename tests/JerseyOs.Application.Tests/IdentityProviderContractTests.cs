using JerseyOs.Application;

namespace JerseyOs.Application.Tests;

public sealed class IdentityProviderContractTests
{
    [Fact]
    public async Task ExternalAndPasswordlessProvidersAreExplicitlyUnsupportedByDefault()
    {
        var external = new UnsupportedExternalIdentityProvider();
        var passwordless = new UnsupportedPasswordlessIdentityProvider();

        await Assert.ThrowsAsync<NotSupportedException>(() =>
            external.AuthenticateAsync("code", "https://localhost/callback", CancellationToken.None));
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            passwordless.RequestSignInTokenAsync("admin@example.invalid", CancellationToken.None));
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            passwordless.CompleteSignInAsync("token", CancellationToken.None));
    }
}
