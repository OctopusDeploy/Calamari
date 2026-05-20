using System;
using Calamari.Aws.Deployment;
using Calamari.Aws.Inputs;
using Calamari.Aws.Integration.Ecs;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.AWS.Inputs;

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
    
    
    // Test Helpers
    static CalamariVariables MinimumRequiredVariableSet()
    {
        return new CalamariVariables
        {
            { AwsSpecialVariables.Ecs.ClusterName, "MyCluster" },
            { DeploymentEnvironment.Id, "Environment-1"},
            { AwsSpecialVariables.Ecs.Deploy.ServiceTaskName, "TestEcsTask"}
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
