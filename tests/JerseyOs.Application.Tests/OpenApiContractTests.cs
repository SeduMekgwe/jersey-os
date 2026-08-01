namespace JerseyOs.Application.Tests;

public sealed class OpenApiContractTests
{
    [Fact]
    public void CommittedOpenApiContainsFoundationOperations()
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "docs", "api", "openapi.yaml"));
        Assert.True(File.Exists(path), $"Missing OpenAPI contract at {path}");
        var document = File.ReadAllText(path);
        Assert.Contains("/auth/login:", document, StringComparison.Ordinal);
        Assert.Contains("/auth/refresh:", document, StringComparison.Ordinal);
        Assert.Contains("/auth/logout:", document, StringComparison.Ordinal);
        Assert.Contains("/auth/me:", document, StringComparison.Ordinal);
        Assert.Contains("/system/health:", document, StringComparison.Ordinal);
        Assert.Contains("operationId: login", document, StringComparison.Ordinal);
        Assert.Contains("UserResponse", document, StringComparison.Ordinal);
    }
}
