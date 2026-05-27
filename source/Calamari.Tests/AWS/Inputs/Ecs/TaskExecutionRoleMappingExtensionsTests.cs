using Amazon.CDK;
using Calamari.Aws.Deployment;
using Calamari.Aws.Inputs.Ecs;
using Calamari.Aws.Integration.Ecs;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using FluentAssertions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.AWS.Inputs.Ecs;

[TestFixture]
public class TaskExecutionRoleMappingExtensionsTests
{
    readonly ILog fakeLog = Substitute.For<ILog>();
    readonly IEcsStackNameGenerator fakeStackNameGenerator = Substitute.For<IEcsStackNameGenerator>();

    [Test]
    public void MapTaskExecutionRoleArn_WhenTaskExecutionRoleSupplied_ReturnsSuppliedArnVerbatim()
    {
        const string suppliedArn = "arn:aws:iam::123456789012:role/MyCustomExecutionRole";
        var variables = MinimumRequiredVariableSet();
        variables[AwsSpecialVariables.Ecs.Deploy.TaskExecutionRole] = suppliedArn;
        var inputs = new DeployEcsCommandInputs(variables, fakeStackNameGenerator, fakeLog);

        var app = new App();
        var stack = new Stack(app, "TestStack");

        var result = inputs.MapTaskExecutionRoleArn(stack);

        result.Should().Be(suppliedArn);
    }

    [Test]
    public void MapTaskExecutionRoleArn_WhenTaskExecutionRoleSupplied_DoesNotCreateRoleOrPolicyArnParameter()
    {
        var variables = MinimumRequiredVariableSet();
        variables[AwsSpecialVariables.Ecs.Deploy.TaskExecutionRole] = "arn:aws:iam::123:role/foo";
        var inputs = new DeployEcsCommandInputs(variables, fakeStackNameGenerator, fakeLog);

        var app = new App();
        var stack = new Stack(app, "TestStack");

        inputs.MapTaskExecutionRoleArn(stack);

        // CDK always injects a BootstrapVersion parameter, so we can't assert Parameters is empty.
        // Instead, assert our specific parameter and resource are absent.
        var template = SynthTemplate(app, "TestStack");
        template["Parameters"]?["AmazonECSTaskExecutionRolePolicyArn"].Should().BeNull();
        template["Resources"]?[inputs.FallbackTaskExecutionRoleName].Should().BeNull();
    }

    [Test]
    public void MapTaskExecutionRoleArn_WhenTaskExecutionRoleEmpty_ReturnsCfnReferenceToken()
    {
        var inputs = new DeployEcsCommandInputs(MinimumRequiredVariableSet(), fakeStackNameGenerator, fakeLog);

        var app = new App();
        var stack = new Stack(app, "TestStack");

        var result = inputs.MapTaskExecutionRoleArn(stack);

        result.Should().NotBeNullOrEmpty();
        // CDK Ref tokens are unresolved at this point — they start with "${Token[" until synthesis.
        result.Should().StartWith("${Token[");
    }

    [Test]
    public void MapTaskExecutionRoleArn_WhenTaskExecutionRoleEmpty_AddsRoleAndPolicyArnParameterToScope()
    {
        var inputs = new DeployEcsCommandInputs(MinimumRequiredVariableSet(), fakeStackNameGenerator, fakeLog);

        var app = new App();
        var stack = new Stack(app, "TestStack");

        inputs.MapTaskExecutionRoleArn(stack);

        var template = SynthTemplate(app, "TestStack");
        template["Parameters"]?["AmazonECSTaskExecutionRolePolicyArn"].Should().NotBeNull();
        template["Resources"]?[inputs.FallbackTaskExecutionRoleName].Should().NotBeNull();
        template["Resources"]?[inputs.FallbackTaskExecutionRoleName]?["Type"]?.Value<string>()
                                .Should().Be("AWS::IAM::Role");
    }

    static JObject SynthTemplate(App app, string stackName)
    {
        var template = app.Synth().GetStackByName(stackName).Template;
        return JObject.Parse(JsonConvert.SerializeObject(template));
    }

    static CalamariVariables MinimumRequiredVariableSet()
    {
        return new CalamariVariables
        {
            { AwsSpecialVariables.Ecs.ClusterName, "MyCluster" },
            { DeploymentEnvironment.Id, "Environment-1" },
            { AwsSpecialVariables.Ecs.Deploy.ServiceTaskName, "TestEcsTask" },
            { AwsSpecialVariables.Ecs.Deploy.Cpu, "2" },
            { AwsSpecialVariables.Ecs.Deploy.Memory, "1" },
            { AwsSpecialVariables.Ecs.Deploy.RuntimeArchitecturePlatform, "X86_64" },
            { AwsSpecialVariables.Ecs.Deploy.DesiredCount, "1" },
            { AwsSpecialVariables.Ecs.Deploy.MinimumHealthPercent, "100" },
            { AwsSpecialVariables.Ecs.Deploy.MaximumHealthPercent, "200" },
            { AwsSpecialVariables.Ecs.Deploy.AutoAssignPublicIp, "False" },
            { AwsSpecialVariables.Ecs.Deploy.EnableEcsManagedTags, "False" },
            { AwsSpecialVariables.Ecs.WaitOption, """{ "type": "waitWithTimeout", "timeout": 30 }""" },
            { AwsSpecialVariables.Ecs.Deploy.SecurityGroupIds, """["sg-0d5e06a4bde84dabc"]""" },
            { AwsSpecialVariables.Ecs.Deploy.SubnetIds, """["subnet-0650cd8a2119e8abc"]""" }
        };
    }
}
