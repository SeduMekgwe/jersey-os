using JerseyOs.Application;
using JerseyOs.Domain;
using JerseyOs.Infrastructure;

namespace JerseyOs.Infrastructure.IntegrationTests;

public sealed class AiContentGeneratorTests
{
    [Fact]
    public async Task FixtureGenerator_ReturnsDeterministicCopyForKind()
    {
        var result = await new FixtureAiContentGenerator().GenerateAsync(
            new AiGenerateRequest(
                AiContentKinds.Title,
                "You write titles.",
                "Product: Home Kit. Team: Arsenal."),
            CancellationToken.None);

        Assert.Equal("fixture", result.Model);
        Assert.Equal(0m, result.EstimatedCostUsd);
        Assert.Contains("Home Kit", result.OutputText, StringComparison.Ordinal);
        Assert.Contains("Fixture title", result.OutputText, StringComparison.Ordinal);
    }
}
