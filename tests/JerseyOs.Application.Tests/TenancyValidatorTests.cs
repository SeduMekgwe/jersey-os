using FluentValidation.TestHelper;
using JerseyOs.Application;
using JerseyOs.Domain;

namespace JerseyOs.Application.Tests;

public sealed class ProvisionOrganizationValidatorTests
{
    private readonly ProvisionOrganizationValidator _validator = new();

    [Fact]
    public void RejectsEmptyFields()
    {
        var result = _validator.TestValidate(
            new ProvisionOrganizationCommand("", "", "", "", null));
        result.ShouldHaveValidationErrorFor(x => x.Name);
        result.ShouldHaveValidationErrorFor(x => x.Slug);
        result.ShouldHaveValidationErrorFor(x => x.AdminEmail);
        result.ShouldHaveValidationErrorFor(x => x.AdminPassword);
    }

    [Fact]
    public void AcceptsValidProvision()
    {
        var result = _validator.TestValidate(
            new ProvisionOrganizationCommand(
                "Acme Kits",
                "acme-kits",
                "admin@acme.invalid",
                "Str0ng!Passw0rd",
                "ZAR"));
        result.ShouldNotHaveAnyValidationErrors();
    }
}

public sealed class CreateOrganizationInvitationValidatorTests
{
    private readonly CreateOrganizationInvitationValidator _validator = new();

    [Fact]
    public void RejectsUnknownRole()
    {
        var result = _validator.TestValidate(
            new CreateOrganizationInvitationCommand("a@b.invalid", "Owner"));
        result.ShouldHaveValidationErrorFor(x => x.Role);
    }

    [Fact]
    public void AcceptsMemberRole()
    {
        var result = _validator.TestValidate(
            new CreateOrganizationInvitationCommand("a@b.invalid", OrganizationRoles.Member));
        result.ShouldNotHaveAnyValidationErrors();
    }
}
