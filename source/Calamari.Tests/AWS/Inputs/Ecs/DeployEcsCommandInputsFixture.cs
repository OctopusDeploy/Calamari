using System;
using Calamari.Aws.Deployment;
using Calamari.Aws.Inputs.Ecs;
using Calamari.Aws.Integration.Ecs;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using FluentAssertions;
using FluentAssertions.Execution;
using NSubstitute;
using NUnit.Framework;
using Octopus.Calamari.Contracts.Aws.Ecs;

namespace Calamari.Tests.AWS.Inputs.Ecs;

[TestFixture]
public class DeployEcsCommandInputsFixture
{
    
    readonly ILog fakeLog = Substitute.For<ILog>();
    readonly IEcsStackNameGenerator fakeStackNameGenerator = Substitute.For<IEcsStackNameGenerator>();
    
    [Test]
    public void Validate_WithEmptyVariableList_ReturnsFalseWithAllRequiredVariables()
    {
        var variables = new CalamariVariables();
        var inputs = new DeployEcsCommandInputs(variables, fakeStackNameGenerator, fakeLog);
        
        var result = inputs.Validate().IsValid;
        
        result.Should().BeFalse();
    }

    [Test]
    public void Validate_WithMissingRequiredVariables_ReturnsFalse()
    {
        var variables = new CalamariVariables();
        var inputs = new DeployEcsCommandInputs(variables, fakeStackNameGenerator, fakeLog);
        
        var result = inputs.Validate().IsValid;
        
        result.Should().BeFalse();
    }

    [Test]
    public void Validate_WithAllExpectedVariables_ReturnsTrue()
    {
        var inputs = new DeployEcsCommandInputs(MinimumRequiredVariableSet(), fakeStackNameGenerator, fakeLog);
        
        var result = inputs.Validate().IsValid;
        
        result.Should().BeTrue();
    }

    [Test]
    [TestCase(true)]
    [TestCase(false)]
    public void ClusterName_ReturnsEvaluatedClusterName(bool useExpression)
    {
        const string expectedClusterName = "MyTestCluster";
        var variables = SetupVariable(AwsSpecialVariables.Ecs.ClusterName, expectedClusterName, useExpression);
        var inputs = new DeployEcsCommandInputs(variables, fakeStackNameGenerator, fakeLog);

        var clusterName = inputs.ClusterName;
        
        clusterName.Should().Be(expectedClusterName);
    }

    [Test]
    public void CfStackName_WhenNotInVariables_ReturnsValue()
    {
        var inputs = new DeployEcsCommandInputs(MinimumRequiredVariableSet(), fakeStackNameGenerator, fakeLog);

        var stackName = inputs.CfStackName;
        
        stackName.Should().NotBeNullOrEmpty();
    }

    [Test]
    [TestCase(true)]
    [TestCase(false)]
    public void CfStackName_WhenInVariables_ReturnsValue(bool useExpression)
    {
        const string expectedStackName = "MyTestStack";
        var variables = SetupVariable(AwsSpecialVariables.Ecs.Deploy.StackName, expectedStackName, useExpression);
        var inputs = new DeployEcsCommandInputs(variables, fakeStackNameGenerator, fakeLog);
        
        var stackName = inputs.CfStackName;
        
        stackName.Should().Be(expectedStackName);
    }

    [Test]
    public void CfStackName_WhenEmptyString_ReturnGeneratedValue()
    {
        const string expectedStackName = "MyGeneratedStack";
        fakeStackNameGenerator.Generate(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(expectedStackName);
        var variables = SetupVariable(AwsSpecialVariables.Ecs.Deploy.StackName, "", false);
        var inputs = new DeployEcsCommandInputs(variables, fakeStackNameGenerator, fakeLog);
        
        var stackName = inputs.CfStackName;
        
        stackName.Should().Be(expectedStackName);
    }

    [Test]
    public void Environment_ReturnsDeploymentEnvironmentId()
    {
        const string expectedEnvironmentId = "TestEnvironment-1";
        var variables = SetupVariable(DeploymentEnvironment.Id, expectedEnvironmentId, false);
        var inputs = new DeployEcsCommandInputs(variables, fakeStackNameGenerator, fakeLog);
        
        var stackName = inputs.Environment;
        
        stackName.Should().Be(expectedEnvironmentId);
    }

    [Test]
    public void Tenant_WithNoTenantVariable_ReturnsEmptyString()
    {
        var variables = MinimumRequiredVariableSet();
        var inputs = new DeployEcsCommandInputs(variables, fakeStackNameGenerator, fakeLog);
        
        var stackName = inputs.Tenant;
        
        stackName.Should().BeEmpty();
    }

    [Test]
    public void Tenant_WithTenantVariable_ReturnsTenantId()
    {
        const string expectedTenantId = "TestTenant-1";
        var variables = SetupVariable(DeploymentVariables.Tenant.Id, expectedTenantId, false);
        var inputs = new DeployEcsCommandInputs(variables, fakeStackNameGenerator, fakeLog);
        
        var stackName = inputs.Tenant;
        
        stackName.Should().Be(expectedTenantId);
    }

    [Test]
    public void CfStackArn_ReturnsCorrectlyFormattedArnForStackName()
    {
        var variables = MinimumRequiredVariableSet();
        const string expectedStackName = "MyGeneratedStack";
        fakeStackNameGenerator.Generate(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(expectedStackName);
        var inputs = new DeployEcsCommandInputs(variables, fakeStackNameGenerator, fakeLog);
        
        var stackArn = inputs.CfStackArn;
        
        stackArn.Value.Should().EndWith(expectedStackName);
    }

    [Test]
    [TestCase(true)]
    [TestCase(false)]
    public void ServiceName_ReturnsServiceTaskNameValueWithPrefix(bool useExpression)
    {
        const string expectedServiceTaskName = "MyNewEcsService";
        var variables = SetupVariable(AwsSpecialVariables.Ecs.Deploy.ServiceTaskName, expectedServiceTaskName, useExpression);
        var inputs = new DeployEcsCommandInputs(variables, fakeStackNameGenerator, fakeLog);
        
        var serviceName = inputs.ServiceName;
        
        serviceName.Should().Be("ServicemyNewEcsService");
    }
    
    
    [Test]
    [TestCase(true)]
    [TestCase(false)]
    public void TaskName_ReturnsServiceTaskNameValueWithPrefix(bool useExpression)
    {
        const string expectedServiceTaskName = "MyNewEcsServiceTask";
        var variables = SetupVariable(AwsSpecialVariables.Ecs.Deploy.ServiceTaskName, expectedServiceTaskName, useExpression);
        var inputs = new DeployEcsCommandInputs(variables, fakeStackNameGenerator, fakeLog);
        
        var taskName = inputs.TaskName;
        
        taskName.Should().Be("TaskDefinitionmyNewEcsServiceTask");
    }

    [Test]
    [TestCase(true)]
    [TestCase(false)]
    public void Cpu_IsReturnedAsAString(bool useExpression)
    {
        const string cpuInput = "0.5";
        var variables = SetupVariable(AwsSpecialVariables.Ecs.Deploy.Cpu, cpuInput, useExpression);
        var inputs = new DeployEcsCommandInputs(variables, fakeStackNameGenerator, fakeLog);
        
        var cpu = inputs.Cpu;

        cpu.Should().Be("0.5");
    }
    
    [Test]
    [TestCase(true)]
    [TestCase(false)]
    public void Memory_IsReturnedAsAString(bool useExpression)
    {
        const string memoryInput = "0.5";
        var variables = SetupVariable(AwsSpecialVariables.Ecs.Deploy.Memory, memoryInput, useExpression);
        var inputs = new DeployEcsCommandInputs(variables, fakeStackNameGenerator, fakeLog);
        
        var memory = inputs.Memory;

        memory.Should().Be("0.5");
    }

    [Test]
    [TestCase(true)]
    [TestCase(false)]
    public void CpuArchitecture_IsReturnedAsString(bool useExpression)
    {
        const string cpuArchitecture = "ARM64";
        var variables = SetupVariable(AwsSpecialVariables.Ecs.Deploy.RuntimeArchitecturePlatform, cpuArchitecture, useExpression);
        var inputs = new DeployEcsCommandInputs(variables, fakeStackNameGenerator, fakeLog);

        var architecture = inputs.CpuArchitecture;

        architecture.Should().Be("ARM64");
    }
    
    [Test]
    [TestCase(true)]
    [TestCase(false)]
    public void DesiredCount_IsReturnedAsADouble(bool useExpression)
    {
        const string desiredCountInput = "7";
        var variables = SetupVariable(AwsSpecialVariables.Ecs.Deploy.DesiredCount, desiredCountInput, useExpression);
        var inputs = new DeployEcsCommandInputs(variables, fakeStackNameGenerator, fakeLog);
        
        var desiredCount = inputs.DesiredCount;

        desiredCount.Should().Be(7);
    }
    
    [Test]
    [TestCase(true)]
    [TestCase(false)]
    public void MinimumHealthyPercentage_IsReturnedAsADouble(bool useExpression)
    {
        const string minHealthInput = "50";
        var variables = SetupVariable(AwsSpecialVariables.Ecs.Deploy.MinimumHealthPercent, minHealthInput, useExpression);
        var inputs = new DeployEcsCommandInputs(variables, fakeStackNameGenerator, fakeLog);
        
        var result = inputs.MinimumHealthyPercentage;

        result.Should().Be(50);
    }
    
    [Test]
    [TestCase(true)]
    [TestCase(false)]
    public void MaximumHealthyPercentage_IsReturnedAsADouble(bool useExpression)
    {
        const string maxHealthInput = "150";
        var variables = SetupVariable(AwsSpecialVariables.Ecs.Deploy.MaximumHealthPercent, maxHealthInput, useExpression);
        var inputs = new DeployEcsCommandInputs(variables, fakeStackNameGenerator, fakeLog);
        
        var result = inputs.MaximumHealthyPercentage;

        result.Should().Be(150);
    }

    [Test]
    public void WaitOption_IsDeserialisedAndReturned()
    {
        const string waitOptionInput = """{ "type": "waitUntilCompleted" }""";
        var variables = SetupVariable(AwsSpecialVariables.Ecs.WaitOption, waitOptionInput, false);
        var inputs = new DeployEcsCommandInputs(variables, fakeStackNameGenerator, fakeLog);
        
        var result = inputs.WaitOption;
        
        result.Type.Should().Be(WaitType.WaitUntilCompleted);
        result.TimeoutMinutes.Should().BeNull();
    }
    
    [Test]
    [TestCase(true)]
    [TestCase(false)]
    public void AutoAssignPublicIp_IsReturnedAsAString(bool useExpression)
    {
        const string enablePublicIpInput = "True";
        var variables = SetupVariable(AwsSpecialVariables.Ecs.Deploy.AutoAssignPublicIp, enablePublicIpInput, useExpression);
        var inputs = new DeployEcsCommandInputs(variables, fakeStackNameGenerator, fakeLog);
        
        var result = inputs.AutoAssignPublicIp;

        result.Should().Be("ENABLED");
    }
    
    [Test]
    [TestCase(true)]
    [TestCase(false)]
    public void EnableEcsManagedTags_IsReturnedAsABool(bool useExpression)
    {
        const string enableEcsManagedTagsInput = "True";
        var variables = SetupVariable(AwsSpecialVariables.Ecs.Deploy.EnableEcsManagedTags, enableEcsManagedTagsInput, useExpression);
        var inputs = new DeployEcsCommandInputs(variables, fakeStackNameGenerator, fakeLog);
        
        var result = inputs.EnableEcsManagedTags;

        result.Should().BeTrue();
    }

    [Test]
    [TestCase(true)]
    [TestCase(false)]
    public void NetworkSecurityGroupIds_IsReturnedAsAStringArray(bool useExpression)
    {
        const string securityGroupsInput = """"
                                           ["sg-0123abcd456789fgh", "sg-abcd1234abcdef567"]
                                           """";
        var variables = SetupVariable(AwsSpecialVariables.Ecs.Deploy.SecurityGroupIds, securityGroupsInput, useExpression);
        var inputs = new DeployEcsCommandInputs(variables, fakeStackNameGenerator, fakeLog);

        var result = inputs.NetworkSecurityGroupIds;
        
        result.Length.Should().Be(2);
        result.Should().Contain("sg-0123abcd456789fgh");
        result.Should().Contain("sg-abcd1234abcdef567");
    }
    
    [Test]
    [TestCase(true)]
    [TestCase(false)]
    public void SubnetIds_IsReturnedAsAStringArray(bool useExpression)
    {
        const string subnetsInput = """"
                                          ["subnet-0123abcd456789fgh", "subnet-abcd1234abcdef567", "subnet-xxxxxxxxxxxxxxxx"]
                                          """";
        var variables = SetupVariable(AwsSpecialVariables.Ecs.Deploy.SubnetIds, subnetsInput, useExpression);
        var inputs = new DeployEcsCommandInputs(variables, fakeStackNameGenerator, fakeLog);

        var result = inputs.SubnetIDs;
        
        result.Length.Should().Be(3);
        result.Should().Contain("subnet-0123abcd456789fgh");
        result.Should().Contain("subnet-abcd1234abcdef567");
        result.Should().Contain("subnet-xxxxxxxxxxxxxxxx");
    }

    [Test]
    public void TaskRole_WithValueUnspecified_ReturnsEmptyString()
    {
        var inputs = new DeployEcsCommandInputs(MinimumRequiredVariableSet(), fakeStackNameGenerator, fakeLog);

        var roleId = inputs.TaskRole;

        roleId.Should().BeEmpty();
    }
    
    [Test]
    public void TaskExecutionRole_WithValueUnspecified_ReturnsEmptyString()
    {
        var inputs = new DeployEcsCommandInputs(MinimumRequiredVariableSet(), fakeStackNameGenerator, fakeLog);

        var roleId = inputs.TaskExecutionRole;

        roleId.Should().BeEmpty();
    }
    
    [Test]
    [TestCase(true)]
    [TestCase(false)]
    public void TaskRole_ReturnsSuppliedValue(bool useExpression)
    {
        var taskRoleArn = "arn:aws:iam::123456780912:role/ecsTaskRole";
        var variables = SetupVariable(AwsSpecialVariables.Ecs.Deploy.TaskRole, taskRoleArn, useExpression);
        var inputs = new DeployEcsCommandInputs(variables, fakeStackNameGenerator, fakeLog);

        var roleId = inputs.TaskRole;

        roleId.Should().Be(taskRoleArn);
    }
    
    [Test]
    [TestCase(true)]
    [TestCase(false)]
    public void TaskExecutionRole_ReturnsSuppliedValue(bool useExpression)
    {
        // { AwsSpecialVariables.Ecs.Deploy.TaskExecutionRole, "arn:aws:iam::123456780912:role/ecsTaskRole"}
        var taskExecRoleArn = "arn:aws:iam::123456780912:role/ecsExecTaskRole";
        var variables = SetupVariable(AwsSpecialVariables.Ecs.Deploy.TaskExecutionRole, taskExecRoleArn, useExpression);
        var inputs = new DeployEcsCommandInputs(variables, fakeStackNameGenerator, fakeLog);

        var roleId = inputs.TaskExecutionRole;

        roleId.Should().Be(taskExecRoleArn);
    }
    
    [Test]
    [TestCase(true)]
    [TestCase(false)]
    public void FallbackTaskExecutionRoleName_ReturnsServiceTaskNameValueWithPrefix(bool useExpression)
    {
        const string serviceTaskName = "MyNewEcsServiceTask";
        var variables = SetupVariable(AwsSpecialVariables.Ecs.Deploy.ServiceTaskName, serviceTaskName, useExpression);
        var inputs = new DeployEcsCommandInputs(variables, fakeStackNameGenerator, fakeLog);
        
        var taskExecutionRoleName = inputs.FallbackTaskExecutionRoleName;
        
        taskExecutionRoleName.Should().Be("TaskExecutionRolemyNewEcsServiceTask");
    }

    [Test]
    [TestCase(true)]
    [TestCase(false)]
    public void ServiceTaskName_ReturnsRawNonPrefixedNameValue(bool useExpression)
    {
        const string expectedServiceTaskName = "ServiceTaskName";
        var variables = SetupVariable(AwsSpecialVariables.Ecs.Deploy.ServiceTaskName, expectedServiceTaskName, useExpression);
        var inputs = new DeployEcsCommandInputs(variables, fakeStackNameGenerator, fakeLog);

        var serviceTaskName = inputs.ServiceTaskName;
        
        serviceTaskName.Should().Be(expectedServiceTaskName);
    }

    [Test]
    public void Containers_ReturnsListOfMappedContainers()
    {

        const string containerJson = """
                                     [{"containerName":"sample-container","containerImageReference":{"referenceId":"547c5091-b891-4bb2-a582-78489bd9b18c","imageName":"#{Octopus.Action.Package[nginx].Image}","feedId":"Feeds-1001"},"repositoryAuthentication":{"type":"default"},"containerPortMappings":[{"containerPort":80,"protocol":"tcp"}],"essential":"True","environmentFiles":[],"environmentVariables":[],"networkSettings":{"disableNetworking":false,"dnsServers":[],"dnsSearchDomains":[],"extraHosts":[]},"containerStorage":{"readOnlyRootFileSystem":"False","mountPoints":[],"volumeFrom":[]},"containerLogging":{"type":"manual","logDriver":"none","logOptions":[]},"firelensConfiguration":{"type":"disabled"},"dockerLabels":[],"healthCheck":{"command":[]},"dependencies":[],"ulimits":[]}]
                                     """;
        var variables = SetupVariable(AwsSpecialVariables.Ecs.Deploy.Containers, containerJson, false);
        variables["Octopus.Action.Package[nginx].Image"] = "docker.io/nginx:1.29.1";
        var inputs = new DeployEcsCommandInputs(variables, fakeStackNameGenerator, fakeLog);
        
        var containers = inputs.Containers;
        containers.Length.Should().Be(1);

        using (new AssertionScope())
        {
            var container = containers[0];
            container.ContainerName.Should().Be("sample-container");
            container.ContainerImageReference.ImageName.Should().Be("docker.io/nginx:1.29.1");
            container.ContainerPortMappings[0].ContainerPort.Should().Be("80");
            container.ContainerPortMappings[0].Protocol.Should().Be(PortProtocol.Tcp);
            container.Essential.Should().Be(true.ToString());

        }

    }

    
    // Test Helpers
    static CalamariVariables MinimumRequiredVariableSet()
    {
        return new CalamariVariables
        {
            { AwsSpecialVariables.Ecs.ClusterName, "MyCluster" },
            { DeploymentEnvironment.Id, "Environment-1"},
            { AwsSpecialVariables.Ecs.Deploy.ServiceTaskName, "TestEcsTask"},
            { AwsSpecialVariables.Ecs.Deploy.Cpu, "2"},
            { AwsSpecialVariables.Ecs.Deploy.Memory, "1"},
            { AwsSpecialVariables.Ecs.Deploy.RuntimeArchitecturePlatform, "X86_64"},
            { AwsSpecialVariables.Ecs.Deploy.DesiredCount, "1"},
            { AwsSpecialVariables.Ecs.Deploy.MinimumHealthPercent, "100"},
            { AwsSpecialVariables.Ecs.Deploy.MaximumHealthPercent, "200"},
            { AwsSpecialVariables.Ecs.Deploy.AutoAssignPublicIp, "False"},
            { AwsSpecialVariables.Ecs.Deploy.EnableEcsManagedTags, "False"},
            { AwsSpecialVariables.Ecs.WaitOption, """{ "type": "waitWithTimeout", "timeout": 30 }"""},
            { AwsSpecialVariables.Ecs.Deploy.SecurityGroupIds, """"
                                                               ["sg-0d5e06a4bde84dabc"],
                                                               """"},
            { AwsSpecialVariables.Ecs.Deploy.SubnetIds, """
                                                        ["subnet-0650cd8a2119e8abc"]
                                                        """},


            
        };
    }
    
    static CalamariVariables SetupVariable(string key, string value, bool useExpression)
    {
        var minimumVariables = MinimumRequiredVariableSet();
        
        if (useExpression)
        {
            const string boundPropertyKey = "BoundPropertyKey";
            const string boundPropertyExpression = $"#{{{boundPropertyKey}}}";

            minimumVariables[key] = boundPropertyExpression;
            minimumVariables[boundPropertyKey] = value;
            
        }
        else
        {
            minimumVariables[key] = value;
        }

        return minimumVariables;
    }
}
