using JerseyOs.Api;
using JerseyOs.Application;
using JerseyOs.Domain;
using JerseyOs.Infrastructure;
using NetArchTest.Rules;

namespace JerseyOs.Architecture.Tests;

public sealed class DependencyRulesTests
{
    [Fact]
    public void DomainDoesNotDependOnOuterLayers()
    {
        var result = Types.InAssembly(typeof(Organization).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "JerseyOs.Application",
                "JerseyOs.Infrastructure",
                "JerseyOs.Api",
                "JerseyOs.Worker")
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join(Environment.NewLine, result.FailingTypeNames ?? []));
    }

    [Fact]
    public void ApplicationDoesNotDependOnInfrastructureOrHosts()
    {
        var result = Types.InAssembly(typeof(LoginCommand).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny("JerseyOs.Infrastructure", "JerseyOs.Api", "JerseyOs.Worker")
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join(Environment.NewLine, result.FailingTypeNames ?? []));
    }

    [Fact]
    public void ApiControllersDoNotDependOnDbContext()
    {
        var result = Types.InAssembly(typeof(Program).Assembly)
            .That()
            .ResideInNamespace("JerseyOs.Api")
            .And()
            .HaveNameEndingWith("Controller")
            .ShouldNot()
            .HaveDependencyOn(typeof(JerseyOsDbContext).FullName!)
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join(Environment.NewLine, result.FailingTypeNames ?? []));
    }
}
