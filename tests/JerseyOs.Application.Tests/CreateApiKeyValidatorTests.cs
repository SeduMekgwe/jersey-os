using FluentValidation.TestHelper;
using JerseyOs.Application;

namespace JerseyOs.Application.Tests;

public sealed class CreateApiKeyValidatorTests
{
    private readonly CreateApiKeyValidator _validator = new();

    [Fact]
    public void RejectsEmptyNameAndScopes()
    {
        var result = _validator.TestValidate(new CreateApiKeyCommand("", []));
        result.ShouldHaveValidationErrorFor(x => x.Name);
        result.ShouldHaveValidationErrorFor(x => x.Scopes);
    }

    [Fact]
    public void AcceptsNamedScopes()
    {
        var result = _validator.TestValidate(
            new CreateApiKeyCommand("Partner", ["catalog.read", "import.upload"]));
        result.ShouldNotHaveAnyValidationErrors();
    }
}
