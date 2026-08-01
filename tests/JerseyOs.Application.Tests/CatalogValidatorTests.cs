using FluentValidation.TestHelper;
using JerseyOs.Application;

namespace JerseyOs.Application.Tests;

public sealed class CatalogValidatorTests
{
    [Fact]
    public void CreateProductRejectsInvalidSlug()
    {
        var result = new CreateProductValidator().TestValidate(
            new CreateProductCommand("Kit", "Bad Slug", null, null, null, null, null));
        result.ShouldHaveValidationErrorFor(x => x.Slug);
    }

    [Fact]
    public void AdjustInventoryRejectsZeroDelta()
    {
        var result = new AdjustInventoryValidator().TestValidate(
            new AdjustInventoryCommand(Guid.NewGuid(), 0, "noop", null));
        result.ShouldHaveValidationErrorFor(x => x.DeltaOnHand);
    }
}
