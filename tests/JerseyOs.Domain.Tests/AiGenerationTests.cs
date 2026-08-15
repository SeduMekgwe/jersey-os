using JerseyOs.Domain;

namespace JerseyOs.Domain.Tests;

public sealed class AiGenerationTests
{
    [Fact]
    public void PromptTemplateRendersPlaceholders()
    {
        var template = new AiPromptTemplate(
            Guid.NewGuid(),
            "Title",
            AiContentKinds.Title,
            "You write product titles.",
            "Write a title for {{name}} ({{team}} {{season}}).");

        var rendered = template.RenderUserPrompt(new Dictionary<string, string?>
        {
            ["name"] = "Home Kit",
            ["team"] = "Arsenal",
            ["season"] = "2025-26"
        });

        Assert.Equal("Write a title for Home Kit (Arsenal 2025-26).", rendered);
    }

    [Fact]
    public void GenerationRequiresApproveBeforeApply()
    {
        var now = DateTimeOffset.UtcNow;
        var generation = new AiGeneration(
            Guid.NewGuid(),
            AiContentKinds.Title,
            AiTargetTypes.Product,
            Guid.NewGuid(),
            """{"name":"Home Kit"}""",
            null,
            "corr");

        Assert.Throws<InvalidOperationException>(() => generation.MarkApplied(now));
        generation.MarkSucceeded("Arsenal Home Kit 2025-26", "fixture", 10, 8, 0m, now);
        generation.Approve();
        generation.MarkApplied(now);
        Assert.Equal(AiGenerationStatus.Applied, generation.Status);
        Assert.Equal("Arsenal Home Kit 2025-26", generation.OutputText);
    }

    [Fact]
    public void UnknownKindIsRejected()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new AiGeneration(
                Guid.NewGuid(),
                "image",
                AiTargetTypes.Product,
                Guid.NewGuid(),
                "{}",
                null,
                "corr"));
    }
}
