using JerseyOs.Domain;

namespace JerseyOs.Domain.Tests;

public sealed class ApiKeyCredentialTests
{
    [Fact]
    public void CreateStoresNormalizedScopesAndPrefix()
    {
        var key = new ApiKeyCredential(
            Guid.NewGuid(),
            "Partner feed",
            "jos_ab12cd34",
            new byte[32],
            ["Import.Upload", "catalog.read"],
            Guid.NewGuid());

        Assert.Equal("jos_ab12cd34", key.Prefix);
        Assert.Equal("catalog.read,import.upload", key.Scopes);
        Assert.True(key.IsUsable);
    }

    [Fact]
    public void RevokeIsIdempotent()
    {
        var key = new ApiKeyCredential(
            Guid.NewGuid(),
            "Partner feed",
            "jos_ab12cd34",
            new byte[32],
            ["catalog.read"],
            null);
        var now = DateTimeOffset.UtcNow;
        key.Revoke(now);
        key.Revoke(now.AddMinutes(1));
        Assert.Equal(now, key.RevokedAtUtc);
        Assert.False(key.IsUsable);
    }

    [Fact]
    public void RejectsEmptyScopes()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new ApiKeyCredential(Guid.NewGuid(), "x", "jos_ab12cd34", new byte[32], [], null));
    }
}
