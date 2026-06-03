using Calamari.Aws.Integration.Ecs;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.AWS.Ecs;

[TestFixture]
public class EcsStackNameGeneratorTests
{
    [Test]
    public void UntenantedDefault_WhenTenantIdMissing()
    {
        var name = new EcsStackNameGenerator().Generate("mycluster", "myservice", "env1");

        name.Should().Be("cf-ecs-mycluster-myservice-env1-untenanted");
    }

    [Test]
    public void UsesTenantId_WhenPresent()
    {
        var name = new EcsStackNameGenerator().Generate("mycluster", "myservice", "env1", "tenant1");

        name.Should().Be("cf-ecs-mycluster-myservice-env1-tenant1");
    }

    [Test]
    public void TruncatesTo128Chars()
    {
        var longService = new string('a', 200);
        var name = new EcsStackNameGenerator().Generate( "cluster", longService, "env1" );

        name.Length.Should().Be(128);
        name.Should().StartWith("cf-ecs-cluster-");
    }
}
