using Calamari.Aws.Integration.Ecs;
using Calamari.Common.Plumbing.Variables;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.AWS.Ecs;

[TestFixture]
public class EcsStackNameTests
{
    [Test]
    public void Generate_UntenantedDefault_WhenTenantIdMissing()
    {
        var variables = new CalamariVariables();
        variables.Set("Octopus.Environment.Id", "env1");

        var name = EcsStackName.Generate(variables, "mycluster", "myservice");

        name.Should().Be("cf-ecs-mycluster-myservice-env1-untenanted");
    }

    [Test]
    public void Generate_UsesTenantId_WhenPresent()
    {
        var variables = new CalamariVariables();
        variables.Set("Octopus.Environment.Id", "env1");
        variables.Set("Octopus.Deployment.Tenant.Id", "tenant1");

        var name = EcsStackName.Generate(variables, "mycluster", "myservice");

        name.Should().Be("cf-ecs-mycluster-myservice-env1-tenant1");
    }

    [Test]
    public void Generate_TruncatesTo128Chars()
    {
        var variables = new CalamariVariables();
        variables.Set("Octopus.Environment.Id", "env1");

        var longService = new string('a', 200);
        var name = EcsStackName.Generate(variables, "cluster", longService);

        name.Length.Should().Be(128);
        name.Should().StartWith("cf-ecs-cluster-");
    }
}
