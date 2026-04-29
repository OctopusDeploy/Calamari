using System;
using Calamari.Aws.Deployment;
using Calamari.Aws.Integration.Ecs.Update;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Variables;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.AWS.Ecs.Update;

[TestFixture]
public class EcsUpdateServiceInputsTests
{
    static CalamariVariables MinimalValidVariables()
    {
        var v = new CalamariVariables();
        v.Set(AwsSpecialVariables.Ecs.ClusterName, "cluster-x");
        v.Set(AwsSpecialVariables.Ecs.ServiceName, "svc-x");
        v.Set(AwsSpecialVariables.Ecs.TargetTaskDefinitionName, "family-x");
        v.Set(AwsSpecialVariables.Ecs.Containers, "[]");
        v.Set(AwsSpecialVariables.Ecs.WaitOption.Type, "dontWait");
        return v;
    }

    [Test]
    public void Parse_ClusterNameMissing_Throws()
    {
        var v = MinimalValidVariables();
        v.Set(AwsSpecialVariables.Ecs.ClusterName, "");

        var act = () => EcsUpdateServiceInputs.Parse(v);

        act.Should().Throw<CommandException>().WithMessage("*Cluster name*");
    }

    [Test]
    public void Parse_TemplateFamilyOmitted_DefaultsToTargetFamily()
    {
        var v = MinimalValidVariables();
        v.Set(AwsSpecialVariables.Ecs.Containers, """[{"ContainerName":"c1"}]""");

        var inputs = EcsUpdateServiceInputs.Parse(v);

        inputs.TemplateTaskDefinitionName.Should().Be(inputs.TargetTaskDefinitionName);
    }

    [Test]
    public void Parse_NoContainers_Throws()
    {
        var v = MinimalValidVariables();

        var act = () => EcsUpdateServiceInputs.Parse(v);

        act.Should().Throw<CommandException>().WithMessage("*at least one container*");
    }

    [Test]
    public void Parse_DuplicateContainerName_Throws()
    {
        var v = MinimalValidVariables();
        v.Set(AwsSpecialVariables.Ecs.Containers, """[{"ContainerName":"c1"},{"ContainerName":"c1"}]""");

        var act = () => EcsUpdateServiceInputs.Parse(v);

        act.Should().Throw<CommandException>().WithMessage("*Duplicate container name*");
    }

    [Test]
    public void Parse_DuplicateTagKey_Throws()
    {
        var v = MinimalValidVariables();
        v.Set(AwsSpecialVariables.Ecs.Containers, """[{"ContainerName":"c1"}]""");
        v.Set(AwsSpecialVariables.Ecs.Tags, """[{"Key":"a","Value":"1"},{"Key":"a","Value":"2"}]""");

        var act = () => EcsUpdateServiceInputs.Parse(v);

        act.Should().Throw<CommandException>().WithMessage("*Duplicate tag key*");
    }

    [Test]
    public void Parse_WaitOptionInvalid_Throws()
    {
        var v = MinimalValidVariables();
        v.Set(AwsSpecialVariables.Ecs.Containers, """[{"ContainerName":"c1"}]""");
        v.Set(AwsSpecialVariables.Ecs.WaitOption.Type, "frobnicate");

        var act = () => EcsUpdateServiceInputs.Parse(v);

        act.Should().Throw<CommandException>().WithMessage("*invalid value*");
    }

    [Test]
    public void Parse_WaitWithTimeoutWithoutTimeout_Throws()
    {
        var v = MinimalValidVariables();
        v.Set(AwsSpecialVariables.Ecs.Containers, """[{"ContainerName":"c1"}]""");
        v.Set(AwsSpecialVariables.Ecs.WaitOption.Type, "waitWithTimeout");

        var act = () => EcsUpdateServiceInputs.Parse(v);

        act.Should().Throw<CommandException>().WithMessage("*timeout value is not set*");
    }

    [Test]
    public void Parse_WaitWithTimeout_PopulatesTimeoutAsTimeSpan()
    {
        var v = MinimalValidVariables();
        v.Set(AwsSpecialVariables.Ecs.Containers, """[{"ContainerName":"c1"}]""");
        v.Set(AwsSpecialVariables.Ecs.WaitOption.Type, "waitWithTimeout");
        v.Set(AwsSpecialVariables.Ecs.WaitOption.Timeout, "60000");

        var inputs = EcsUpdateServiceInputs.Parse(v);

        inputs.WaitOption.Should().Be(WaitOptionType.WaitWithTimeout);
        inputs.WaitTimeout.Should().Be(TimeSpan.FromMinutes(1));
    }
}
