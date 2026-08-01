using FluentValidation.TestHelper;
using JerseyOs.Application;

namespace JerseyOs.Application.Tests;

public sealed class LoginValidatorTests
{
    private readonly LoginValidator _validator = new();

    [Fact]
    public void RejectsEmptyCredentials()
    {
        var result = _validator.TestValidate(new LoginCommand(string.Empty, string.Empty));
        result.ShouldHaveValidationErrorFor(x => x.Email);
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void AcceptsValidCredentials()
    {
        var result = _validator.TestValidate(new LoginCommand("admin@example.invalid", "Str0ng!Passw0rd"));
        result.ShouldNotHaveAnyValidationErrors();
    }
}
