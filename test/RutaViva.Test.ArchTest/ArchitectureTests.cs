using FluentAssertions;
using NetArchTest.Rules;

namespace RutaViva.Test.ArchTest;

/// <summary>The dependency rule of Clean Architecture, checked automatically on every build.</summary>
public class ArchitectureTests
{
    private const string Domain = "RutaViva.Domain";
    private const string Application = "RutaViva.Application";
    private const string Infrastructure = "RutaViva.Infrastructure";
    private const string Api = "RutaViva.Api";
    private const string Worker = "RutaViva.Worker";

    [Fact]
    public void Domain_DependsOnNoOtherLayerNorFramework()
    {
        var result = Types.InAssembly(typeof(Domain.Common.Entity).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(Application, Infrastructure, Api, Worker, "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "Npgsql")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Describe(result));
    }

    [Fact]
    public void Application_DoesNotDependOnInfrastructureNorHosts()
    {
        var result = Types.InAssembly(typeof(Application.RegisterApplicationDI).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(Infrastructure, Api, Worker, "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "Npgsql")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Describe(result));
    }

    [Fact]
    public void Controllers_DoNotDependOnInfrastructure()
    {
        var result = Types.InAssembly(typeof(Api.ErrorHandling.ResultToActionResultMapper).Assembly)
            .That().ResideInNamespace($"{Api}.Controllers")
            .ShouldNot().HaveDependencyOn(Infrastructure)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Describe(result));
    }

    [Fact]
    public void Controllers_LiveInTheControllersNamespace()
    {
        var result = Types.InAssembly(typeof(Api.ErrorHandling.ResultToActionResultMapper).Assembly)
            .That().HaveNameEndingWith("Controller")
            .Should().ResideInNamespace($"{Api}.Controllers")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Describe(result));
    }

    [Fact]
    public void WorkerServices_OnlyUseApplicationAbstractions()
    {
        var result = Types.InAssembly(typeof(Worker.WorkerSettings).Assembly)
            .That().Inherit(typeof(Microsoft.Extensions.Hosting.BackgroundService))
            .ShouldNot().HaveDependencyOn(Infrastructure)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Describe(result));
    }

    private static string Describe(TestResult result) =>
        result.IsSuccessful ? string.Empty : "violations: " + string.Join(", ", result.FailingTypeNames ?? []);
}
