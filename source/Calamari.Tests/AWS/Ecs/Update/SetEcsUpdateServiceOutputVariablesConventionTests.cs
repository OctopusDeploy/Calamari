using Calamari.Aws.Deployment.Conventions;
using Calamari.CloudAccounts;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing.Helpers;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.AWS.Ecs.Update;

[TestFixture]
public class SetEcsUpdateServiceOutputVariablesConventionTests
{
    static AwsEnvironmentGeneration EnvFor(string region)
    {
        var v = new CalamariVariables();
        v.Set("Octopus.Action.Aws.Region", region);
        return new AwsEnvironmentGeneration(new InMemoryLog(), v)
        {
            EnvironmentVars = { ["AWS_REGION"] = region }
        };
    }

    [Test]
    public void Install_SetsAllOutputs_WhenNewRevisionRegistered()
    {
        var log = new InMemoryLog();
        var deployment = new RunningDeployment(new CalamariVariables());
        deployment.Variables.Set(UpdateEcsServiceConvention.OutputFamilyVar, "fam-x");
        deployment.Variables.Set(UpdateEcsServiceConvention.OutputRevisionVar, "7");

        var convention = new SetEcsUpdateServiceOutputVariablesConvention(
            EnvFor("us-east-1"), clusterName: "cluster-x", serviceName: "svc-x", log);

        convention.Install(deployment);

        var msgs = log.Messages.GetServiceMessagesOfType("setVariable");
        msgs.GetPropertyValue("ClusterName").Should().Be("cluster-x");
        msgs.GetPropertyValue("ServiceName").Should().Be("svc-x");
        msgs.GetPropertyValue("Region").Should().Be("us-east-1");
        msgs.GetPropertyValue("TaskDefinitionFamily").Should().Be("fam-x");
        msgs.GetPropertyValue("TaskDefinitionRevision").Should().Be("7");
    }

    [Test]
    public void Install_OmitsRevisionAndFamily_WhenNoNewRevisionRegistered()
    {
        var log = new InMemoryLog();
        var deployment = new RunningDeployment(new CalamariVariables());

        var convention = new SetEcsUpdateServiceOutputVariablesConvention(
            EnvFor("us-east-1"), clusterName: "cluster-x", serviceName: "svc-x", log);

        convention.Install(deployment);

        var msgs = log.Messages.GetServiceMessagesOfType("setVariable");
        msgs.GetPropertyValue("ClusterName").Should().Be("cluster-x");
        msgs.GetPropertyValue("TaskDefinitionFamily").Should().BeNull();
        msgs.GetPropertyValue("TaskDefinitionRevision").Should().BeNull();
    }
}
