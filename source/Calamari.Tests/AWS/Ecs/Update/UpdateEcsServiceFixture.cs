using System;
using System.Threading;
using System.Threading.Tasks;
using Amazon;
using Amazon.ECS;
using Amazon.ECS.Model;
using Amazon.IdentityManagement;
using Amazon.Runtime;
using Calamari.Aws.Commands;
using Calamari.Aws.Deployment;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing;
using Calamari.Testing.Helpers;
using FluentAssertions;
using Newtonsoft.Json;
using NUnit.Framework;
using Octopus.Calamari.Contracts.Aws.Ecs;
using Task = System.Threading.Tasks.Task;

namespace Calamari.Tests.AWS.Ecs.Update;

[TestFixture]
[Category(TestCategory.PlatformAgnostic)]
public class UpdateEcsServiceFixture
{
    // Fixed infrastructure in account 017645897735 (us-east-1)
    const string Region = "us-east-1";
    const string ClusterName = "calamari-ecs-integration-tests";
    const string SubnetId = "subnet-0d3da9354f8253081";
    const string SecurityGroupId = "sg-053ae28309775ea7b";

    const string TaskDefinitionFamily = "calamari-ecs-integration-tests-template";
    const string ExecutionRoleName = "calamari-ecs-integration-tests-execution-role";

    string serviceName;

    [Test]
    public async Task RegistersNewRevisionAndForcesNewDeployment()
    {
        var unique = Guid.NewGuid().ToString("N")[..8];
        serviceName = $"upd-svc-{unique}";

        // Create a fresh service that points at the shared template family. DesiredCount=0 means
        // no Fargate tasks launch (no exec role needed, no compute cost) and the Update step uses
        // dontWait so the watcher returns immediately after UpdateService is acknowledged.
        using var client = await CreateClient();
        await client.CreateServiceAsync(new CreateServiceRequest
        {
            Cluster = ClusterName,
            ServiceName = serviceName,
            TaskDefinition = TaskDefinitionFamily,
            DesiredCount = 0,
            LaunchType = LaunchType.FARGATE,
            NetworkConfiguration = new NetworkConfiguration
            {
                AwsvpcConfiguration = new AwsVpcConfiguration
                {
                    Subnets = [SubnetId],
                    SecurityGroups = [SecurityGroupId],
                    AssignPublicIp = AssignPublicIp.ENABLED
                }
            },
            Tags =
            [
                new Tag { Key = "VantaOwner", Value = "modern-deployments-team@octopus.com" },
                new Tag { Key = "VantaNonProd", Value = "true" },
                new Tag { Key = "VantaNoAlert", Value = "Ephemeral ECS service created during integration tests" },
                new Tag { Key = "VantaContainsUserData", Value = "false" },
                new Tag { Key = "VantaUserDataStored", Value = "N/A" },
                new Tag { Key = "VantaDescription", Value = "Ephemeral ECS service created during integration tests" }
            ]
        });

        var variables = await CreateVariables(serviceName, newImage: "public.ecr.aws/docker/library/nginx:1.28-alpine");
        var log = new InMemoryLog();
        var command = new UpdateEcsServiceCommand(log, variables);
        var result = command.Execute([]);

        result.Should().Be(0);

        var registeredFamily = variables.Get("TaskDefinitionFamily");
        var registeredRevision = variables.Get("TaskDefinitionRevision");

        var serviceResp = await client.DescribeServicesAsync(new DescribeServicesRequest
        {
            Cluster = ClusterName,
            Services = [serviceName]
        });
        var service = serviceResp.Services.Should().ContainSingle().Subject;
        service.TaskDefinition.Should().EndWith($"/{registeredFamily}:{registeredRevision}");
    }

    [Test]
    public async Task FailsWhenTargetTaskDefinitionMissing()
    {
        // No service is created — the convention errors out at the target-family describe before
        // any ECS state is touched. Leaving serviceName empty so TearDown skips its delete call.
        var unique = Guid.NewGuid().ToString("N")[..8];
        var missingTarget = $"calamari-ecs-missing-target-{unique}";

        var variables = await CreateVariables(serviceName: $"unused-{unique}", newImage: "public.ecr.aws/docker/library/nginx:1.28-alpine");
        // Default behavior collapses TemplateTaskDefinitionName to TargetTaskDefinitionName when
        // the former is empty — so we set both explicitly: a known-good template, a known-missing target.
        variables.Set(AwsSpecialVariables.Ecs.Update.TemplateTaskDefinitionName, TaskDefinitionFamily);
        variables.Set(AwsSpecialVariables.Ecs.Update.TargetTaskDefinitionName, missingTarget);

        var log = new InMemoryLog();
        var command = new UpdateEcsServiceCommand(log, variables);

        var act = () => command.Execute([]);
        act.Should().Throw<CommandException>()
            .WithMessage($"*Existing destination task definition '{missingTarget}' not found*");
    }

    static async Task<IVariables> CreateVariables(string serviceName, string newImage)
    {
        var accessKey = await ExternalVariables.Get(ExternalVariable.AwsCloudFormationAndS3AccessKey, CancellationToken.None);
        var secretKey = await ExternalVariables.Get(ExternalVariable.AwsCloudFormationAndS3SecretKey, CancellationToken.None);

        var variables = new CalamariVariables();

        variables.Set("Octopus.Account.AccountType", "AmazonWebServicesAccount");
        variables.Set("Octopus.Action.AwsAccount.Variable", "AWSAccount");
        variables.Set("AWSAccount.AccessKey", accessKey);
        variables.Set("AWSAccount.SecretKey", secretKey);
        variables.Set("Octopus.Action.Aws.Region", Region);
        variables.Set("Octopus.Action.Aws.AssumeRole", "False");
        variables.Set("Octopus.Action.AwsAccount.UseInstanceRole", "False");

        variables.Set("Octopus.Environment.Id", "Environments-1");
        variables.Set("Octopus.Environment.Name", "Test");
        variables.Set("Octopus.Project.Name", "ECS Update Integration Test");
        variables.Set("Octopus.Action.Name", "Update ECS");

        variables.Set(AwsSpecialVariables.Ecs.ClusterName, ClusterName);
        variables.Set(AwsSpecialVariables.Ecs.ServiceName, serviceName);
        variables.Set(AwsSpecialVariables.Ecs.Update.TargetTaskDefinitionName, TaskDefinitionFamily);

        const string packageReference = "web";
        variables.Set(PackageVariables.IndexedImage(packageReference), newImage);

        var containers = new[]
        {
            new ContainerUpdate
            {
                ContainerName = "web",
                PackageReference = packageReference,
                EnvironmentVariables = new EnvAction<TypedKeyValuePair>
                {
                    Action = EnvActionMode.Replace,
                    Items =
                    [
                        new TypedKeyValuePair { Type = KeyValueType.Plain, Key = "LOG_LEVEL", Value = "info" },
                        new TypedKeyValuePair { Type = KeyValueType.Secret, Key = "DB_PASSWORD", Value = "arn:aws:ssm:us-east-1:017645897735:parameter/calamari-ecs-integration-tests-fake" }
                    ]
                }
            }
        };
        variables.Set(AwsSpecialVariables.Ecs.Containers, JsonConvert.SerializeObject(containers));

        var waitOption = new WaitOption { Type = WaitType.DontWait };
        variables.Set(AwsSpecialVariables.Ecs.WaitOption, JsonConvert.SerializeObject(waitOption));

        return variables;
    }

    [OneTimeSetUp]
    public async Task EnsureTaskDefinitionTemplateExists()
    {
        using var client = await CreateClient();
        try
        {
            await client.DescribeTaskDefinitionAsync(new DescribeTaskDefinitionRequest { TaskDefinition = TaskDefinitionFamily });
            return;
        }
        catch (ClientException)
        {
            // Template does not exist so create it below
        }

        // Execution role required to attach secrets
        var executionRoleArn = await EnsureExecutionRole();
        await client.RegisterTaskDefinitionAsync(new RegisterTaskDefinitionRequest
        {
            Family = TaskDefinitionFamily,
            NetworkMode = NetworkMode.Awsvpc,
            RequiresCompatibilities = ["FARGATE"],
            Cpu = "256",
            Memory = "512",
            ExecutionRoleArn = executionRoleArn,
            ContainerDefinitions =
            [
                new ContainerDefinition
                {
                    Name = "web",
                    Image = "public.ecr.aws/docker/library/nginx:1.27-alpine",
                    Essential = true,
                    PortMappings = [new PortMapping { ContainerPort = 80 }]
                }
            ]
        });
    }

    [TearDown]
    public async Task TearDown()
    {
        if (string.IsNullOrEmpty(serviceName))
        {
            return;
        }

        try
        {
            using var client = await CreateClient();
            await client.DeleteServiceAsync(new DeleteServiceRequest
            {
                Cluster = ClusterName,
                Service = serviceName,
                Force = true
            });
        }
        catch (Exception e)
        {
            TestContext.WriteLine($"Failed to delete service '{serviceName}': {e.Message}");
        }
    }

    static async Task<string> EnsureExecutionRole()
    {
        using var iam = await CreateIamClient();
        try
        {
            var resp = await iam.GetRoleAsync(new Amazon.IdentityManagement.Model.GetRoleRequest { RoleName = ExecutionRoleName });
            return resp.Role.Arn;
        }
        catch (Amazon.IdentityManagement.Model.NoSuchEntityException)
        {
            const string trustPolicy = """{"Version":"2012-10-17","Statement":[{"Effect":"Allow","Principal":{"Service":"ecs-tasks.amazonaws.com"},"Action":"sts:AssumeRole"}]}""";
            var createResp = await iam.CreateRoleAsync(new Amazon.IdentityManagement.Model.CreateRoleRequest
            {
                RoleName = ExecutionRoleName,
                AssumeRolePolicyDocument = trustPolicy
            });
            await iam.AttachRolePolicyAsync(new Amazon.IdentityManagement.Model.AttachRolePolicyRequest
            {
                RoleName = ExecutionRoleName,
                PolicyArn = "arn:aws:iam::aws:policy/service-role/AmazonECSTaskExecutionRolePolicy"
            });
            return createResp.Role.Arn;
        }
    }

    static async Task<AmazonECSClient> CreateClient()
    {
        var accessKey = await ExternalVariables.Get(ExternalVariable.AwsCloudFormationAndS3AccessKey, CancellationToken.None);
        var secretKey = await ExternalVariables.Get(ExternalVariable.AwsCloudFormationAndS3SecretKey, CancellationToken.None);
        var credentials = new BasicAWSCredentials(accessKey, secretKey);
        var config = new AmazonECSConfig { RegionEndpoint = RegionEndpoint.GetBySystemName(Region) };
        return new AmazonECSClient(credentials, config);
    }

    static async Task<AmazonIdentityManagementServiceClient> CreateIamClient()
    {
        var accessKey = await ExternalVariables.Get(ExternalVariable.AwsCloudFormationAndS3AccessKey, CancellationToken.None);
        var secretKey = await ExternalVariables.Get(ExternalVariable.AwsCloudFormationAndS3SecretKey, CancellationToken.None);
        var credentials = new BasicAWSCredentials(accessKey, secretKey);
        return new AmazonIdentityManagementServiceClient(credentials, RegionEndpoint.GetBySystemName(Region));
    }
}
