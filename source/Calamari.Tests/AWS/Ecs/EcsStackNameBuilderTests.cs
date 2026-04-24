using Calamari.Aws.Integration.Ecs;
using Calamari.Common.Plumbing.Variables;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.AWS.Ecs;

[TestFixture]
public class EcsStackNameBuilderTests
{
    [Test]
    public void Build_UntenantedDefault_WhenTenantIdMissing()
    {
        var variables = new CalamariVariables();
        variables.Set("Octopus.Environment.Id", "Environments-1");

        var name = EcsStackNameBuilder.Build(variables, "my-cluster", "my-service");

        name.Should().Be("cf-ecs-myCluster-myService-environments1-untenanted");
    }

    [Test]
    public void Build_UsesTenantId_WhenPresent()
    {
        var variables = new CalamariVariables();
        variables.Set("Octopus.Environment.Id", "Environments-2");
        variables.Set("Octopus.Deployment.Tenant.Id", "Tenants-17");

        var name = EcsStackNameBuilder.Build(variables, "my-cluster", "my-service");

        name.Should().Be("cf-ecs-myCluster-myService-environments2-tenants17");
    }

    [Test]
    public void Build_CamelCasesEachSegment_RemovingSeparators()
    {
        var variables = new CalamariVariables();
        variables.Set("Octopus.Environment.Id", "Environments-1");

        var name = EcsStackNameBuilder.Build(variables, "spf-deprecation-cluster", "web app service");

        name.Should().Be("cf-ecs-spfDeprecationCluster-webAppService-environments1-untenanted");
    }

    [Test]
    public void Build_PreservesOrdinalsAsSingleTokens_MatchingLodash()
    {
        var variables = new CalamariVariables();
        variables.Set("Octopus.Environment.Id", "Environments-1");

        var name = EcsStackNameBuilder.Build(variables, "1st-gen-cluster", "cluster-21st");

        // Matches lodash.camelCase output: "1stGenCluster", "cluster21st".
        // Our previous hand-rolled splitter produced "1StGenCluster" and "cluster21St",
        // which would trigger resource renames on SPF → Calamari migration.
        name.Should().Be("cf-ecs-1stGenCluster-cluster21st-environments1-untenanted");
    }

    [Test]
    public void Build_TruncatesTo128Chars()
    {
        var variables = new CalamariVariables();
        variables.Set("Octopus.Environment.Id", "Environments-1");

        var longService = new string('a', 200);
        var name = EcsStackNameBuilder.Build(variables, "cluster", longService);

        name.Length.Should().Be(128);
        name.Should().StartWith("cf-ecs-cluster-");
    }
}
