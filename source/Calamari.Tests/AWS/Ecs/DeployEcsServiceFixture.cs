using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Amazon.Runtime;
using Calamari.Aws.Commands;
using Calamari.Aws.Deployment;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing;
using Calamari.Testing.Helpers;
using FluentAssertions;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Calamari.Tests.AWS.Ecs;

[TestFixture]
[Category(TestCategory.RunOnceOnWindowsAndLinux)]
public class DeployEcsServiceFixture
{
    // Fixed infrastructure in account 017645897735 (us-east-1)
    const string Region = "us-east-1";
    const string ClusterName = "calamari-ecs-integration-tests";
    const string SubnetId = "subnet-0d3da9354f8253081";
    const string SecurityGroupId = "sg-053ae28309775ea7b";

    string stackName;

    [TearDown]
    public async Task TearDown()
    {
        if (!string.IsNullOrEmpty(stackName))
        {
            try
            {
                await DeleteStack(stackName);
            }
            catch (Exception e)
            {
                TestContext.WriteLine($"Failed to clean up stack {stackName}: {e.Message}");
            }
        }
    }

    [Test]
    public async Task DeployEcsService_CreatesCloudFormationStack()
    {
        stackName = GenerateStackName();
        var serviceName = "test-svc";

        var variables = await CreateVariables(serviceName, stackName);
        var log = new InMemoryLog();
        var command = new DeployEcsServiceCommand(log, variables);

        var result = command.Execute(Array.Empty<string>());

        result.Should().Be(0);
        await ValidateStackExists(stackName, true);
    }

    static string GenerateStackName() =>
        $"calamari-ecs-{Guid.NewGuid():N}".Substring(0, 40);


    static string BuildTemplate(string serviceName) => $$"""
        {
          "AWSTemplateFormatVersion": "2010-09-09",
          "Resources": {
            "ExecutionRole": {
              "Type": "AWS::IAM::Role",
              "Properties": {
                "AssumeRolePolicyDocument": {
                  "Version": "2012-10-17",
                  "Statement": [{
                    "Effect": "Allow",
                    "Principal": { "Service": "ecs-tasks.amazonaws.com" },
                    "Action": "sts:AssumeRole"
                  }]
                },
                "ManagedPolicyArns": [
                  "arn:aws:iam::aws:policy/service-role/AmazonECSTaskExecutionRolePolicy"
                ]
              }
            },
            "TaskDefinition": {
              "Type": "AWS::ECS::TaskDefinition",
              "Properties": {
                "Family": "{{serviceName}}",
                "RequiresCompatibilities": ["FARGATE"],
                "NetworkMode": "awsvpc",
                "Cpu": "256",
                "Memory": "512",
                "ExecutionRoleArn": { "Fn::GetAtt": ["ExecutionRole", "Arn"] },
                "ContainerDefinitions": [
                  {
                    "Name": "web",
                    "Image": "public.ecr.aws/docker/library/nginx:alpine",
                    "Essential": true,
                    "PortMappings": [{ "ContainerPort": 80, "Protocol": "tcp" }]
                  }
                ]
              }
            },
            "Service": {
              "Type": "AWS::ECS::Service",
              "Properties": {
                "Cluster": "{{ClusterName}}",
                "ServiceName": "{{serviceName}}",
                "TaskDefinition": { "Ref": "TaskDefinition" },
                "LaunchType": "FARGATE",
                "DesiredCount": 0,
                "NetworkConfiguration": {
                  "AwsvpcConfiguration": {
                    "Subnets": ["{{SubnetId}}"],
                    "SecurityGroups": ["{{SecurityGroupId}}"],
                    "AssignPublicIp": "ENABLED"
                  }
                }
              }
            }
          }
        }
        """;

    static async Task<IVariables> CreateVariables(string serviceName, string cfStackName)
    {
        var accessKey = await ExternalVariables.Get(ExternalVariable.AwsCloudFormationAndS3AccessKey, CancellationToken.None);
        var secretKey = await ExternalVariables.Get(ExternalVariable.AwsCloudFormationAndS3SecretKey, CancellationToken.None);

        var variables = new CalamariVariables();

        // AWS authentication
        variables.Set("Octopus.Account.AccountType", "AmazonWebServicesAccount");
        variables.Set("Octopus.Action.AwsAccount.Variable", "AWSAccount");
        variables.Set("AWSAccount.AccessKey", accessKey);
        variables.Set("AWSAccount.SecretKey", secretKey);
        variables.Set("Octopus.Action.Aws.Region", Region);
        variables.Set("Octopus.Action.Aws.AssumeRole", "False");
        variables.Set("Octopus.Action.AwsAccount.UseInstanceRole", "False");

        // Deployment context (consumed by surviving conventions + log output)
        variables.Set("Octopus.Environment.Id", "Environments-1");
        variables.Set("Octopus.Environment.Name", "Test");
        variables.Set("Octopus.Project.Name", "ECS Integration Test");
        variables.Set("Octopus.Action.Name", "Deploy ECS");

        // CFN inputs (what the server mapper will emit)
        variables.Set(AwsSpecialVariables.CloudFormation.Template, BuildTemplate(serviceName));
        variables.Set(AwsSpecialVariables.CloudFormation.TemplateParameters, "[]");
        variables.Set(AwsSpecialVariables.CloudFormation.StackName, cfStackName);

        // Stack-level tags (Vanta compliance tags that integration infra requires)
        variables.Set(AwsSpecialVariables.CloudFormation.Tags, JsonConvert.SerializeObject(new[]
        {
            new { Key = "VantaOwner", Value = "modern-deployments-team@octopus.com" },
            new { Key = "VantaNonProd", Value = "true" },
            new { Key = "VantaNoAlert", Value = "Ephemeral ECS service created during integration tests" },
            new { Key = "VantaContainsUserData", Value = "false" },
            new { Key = "VantaUserDataStored", Value = "N/A" },
            new { Key = "VantaDescription", Value = "Ephemeral ECS service created during integration tests" }
        }));

        // ECS-specific vars for post-deploy diagnostics
        variables.Set(AwsSpecialVariables.Ecs.ClusterName, ClusterName);
        variables.Set(AwsSpecialVariables.Ecs.ServiceName, serviceName);
        variables.Set(AwsSpecialVariables.Ecs.WaitOption.Type, "waitWithTimeout");
        variables.Set(AwsSpecialVariables.Ecs.WaitOption.Timeout, ((int)TimeSpan.FromMinutes(5).TotalMilliseconds).ToString());

        return variables;
    }

    static async Task ValidateStackExists(string stackName, bool shouldExist)
    {
        var credentials = await GetCredentials();
        var config = new AmazonCloudFormationConfig { RegionEndpoint = RegionEndpoint.GetBySystemName(Region) };

        using var client = new AmazonCloudFormationClient(credentials, config);
        try
        {
            var response = await client.DescribeStacksAsync(new DescribeStacksRequest { StackName = stackName });
            var stack = response.Stacks.FirstOrDefault();

            if (shouldExist)
            {
                stack.Should().NotBeNull($"stack {stackName} should exist");
                stack!.StackStatus.Value.Should().NotContain("FAILED");
            }
            else
            {
                stack?.StackStatus.Value.Should().Be("DELETE_COMPLETE");
            }
        }
        catch (AmazonCloudFormationException ex) when (ex.ErrorCode == "ValidationError")
        {
            if (shouldExist)
                Assert.Fail($"Stack {stackName} does not exist but was expected to.");
        }
    }

    static async Task DeleteStack(string stackName)
    {
        var credentials = await GetCredentials();
        var config = new AmazonCloudFormationConfig { RegionEndpoint = RegionEndpoint.GetBySystemName(Region) };

        using var client = new AmazonCloudFormationClient(credentials, config);
        await client.DeleteStackAsync(new DeleteStackRequest { StackName = stackName });

        // Wait for deletion
        for (var i = 0; i < 30; i++)
        {
            await Task.Delay(TimeSpan.FromSeconds(10));
            try
            {
                var response = await client.DescribeStacksAsync(new DescribeStacksRequest { StackName = stackName });
                var stack = response.Stacks.FirstOrDefault();
                if (stack == null || stack.StackStatus.Value == "DELETE_COMPLETE")
                    return;
            }
            catch (AmazonCloudFormationException ex) when (ex.ErrorCode == "ValidationError")
            {
                return;
            }
        }
    }

    static async Task<BasicAWSCredentials> GetCredentials()
    {
        var accessKey = await ExternalVariables.Get(ExternalVariable.AwsCloudFormationAndS3AccessKey, CancellationToken.None);
        var secretKey = await ExternalVariables.Get(ExternalVariable.AwsCloudFormationAndS3SecretKey, CancellationToken.None);
        return new BasicAWSCredentials(accessKey, secretKey);
    }
}
