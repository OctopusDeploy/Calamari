using Amazon.ECS.Model;
using Calamari.Aws.Discovery;
using Calamari.Aws.Integration.Ecs;
using Calamari.Common.Features.Discovery;
using Calamari.Testing.Helpers;
using FluentAssertions;
using FluentAssertions.Execution;
using NUnit.Framework;
using Octopus.Calamari.Contracts.TargetDiscovery;

namespace Calamari.Tests.AWS.Ecs;

[TestFixture]
public class EcsClusterDiscoveryWriterTests
{
    InMemoryLog log;
    EcsClusterDiscoveryWriter writer;
    Cluster testCluster;
    TargetMatchResult matchResult;
    readonly AwsAccessKeyAuthenticationDetails accessKeyAuth;
    readonly AwsWorkerAuthenticationDetails workerAuth;
    readonly AwsOidcAuthenticationDetails oidcAuth;
    TargetDiscoveryScope scope;

    public EcsClusterDiscoveryWriterTests()
    {
        accessKeyAuth = new AwsAccessKeyAuthenticationDetails
        {
            Credentials = new AwsCredentials<AwsAccessKeyCredentials>
            {
                AccountId = "Account-1",
                Type = "account",
                Account = new AwsAccessKeyCredentials
                {
                    AccessKey = "TestAccessKey",
                    SecretKey = "TestSecretKey",
                }
            },
            Role = new AwsAssumedRole
            {
                Type = "noAssumedRole"
            }
        };

        workerAuth = new AwsWorkerAuthenticationDetails
        {
            Type = "Aws",
            Credentials = new AwsCredentials<AwsWorkerCredentials>()
            {
                Type = "worker",
            },
            Role = new AwsAssumedRole
            {
                Type = "noAssumedRole"
            }
        };

        oidcAuth = new AwsOidcAuthenticationDetails
        {
            Type = "Aws",
            Credentials = new AwsCredentials<AwsOidcCredentials>
            {
                Type= "oidcAccount",
                Account = new AwsOidcCredentials
                {
                    RoleArn = "arn:aws:iam::123456789012:role/TestRole",
                    SessionDuration = "3600",
                    Jwt = "TestJwt",
                },
            },
            Role = new AwsAssumedRole
            {
                Arn = "arn:aws:iam::123456789012:role/TestRole",
                ExternalId = "externalId",
                SessionDuration = 3600,
                SessionName = "TestSessionName",
                Type = "assumeRole",
            }
        };
    }

    [SetUp]
    public void SetUp()
    {
        log = new InMemoryLog();
        writer = new EcsClusterDiscoveryWriter(log);

        testCluster = new Cluster
        {
            ClusterName = "test-cluster",
        };

        matchResult = TargetMatchResult.Success("spf-deprecation-test", null);

        scope = new TargetDiscoveryScope("Space-1",
            "Production",
            "Test-Project",
            null,
            ["spf-deprecation"],
            null,
            null);
    }

    [Test]
    public void WriteTargetCreationServiceMessage_WritesMessageWithPopulatedDefaultVariables()
    {
        // Act
        writer.WriteTargetCreationServiceMessage("ap-southeast-2",
            testCluster,
            accessKeyAuth,
            scope,
            matchResult);
        var serviceMessages = log.Messages.GetServiceMessagesOfType(AwsEcsServiceMessageNames.CreateTargetName);

        // Assert
        using var _ = new AssertionScope();
        serviceMessages.Length.Should().Be(1);

        var message = serviceMessages[0];

        message.Properties[DefaultKeyNames.Name].Should().Be("aws-ecs/ap-southeast-2/test-cluster");
        message.Properties[DefaultKeyNames.IsDynamic].Should().Be("True");
        message.Properties[DefaultKeyNames.UpdateIfExisting].Should().Be("True");
        message.Properties[DefaultKeyNames.OctopusRoles].Should().Be("spf-deprecation-test");

        // Null values are stripped out
        message.Properties.Keys.Should().NotContain(DefaultKeyNames.TenantedDeploymentParticipation);
    }

    [Test]
    public void WriteTargetCreationServiceMessage_WritesMessagesWithEcsClusterSpecificValues()
    {
        // Act
        writer.WriteTargetCreationServiceMessage("ap-southeast-2",
            testCluster,
            accessKeyAuth,
            scope,
            matchResult);
        var serviceMessages = log.Messages.GetServiceMessagesOfType(AwsEcsServiceMessageNames.CreateTargetName);

        // Assert
        using var _ = new AssertionScope();
        serviceMessages.Length.Should().Be(1);
        var message = serviceMessages[0];

        message.Properties[AwsEcsServiceMessageNames.AccountIdOrNameAttribute].Should().Be("Account-1");
        message.Properties[AwsEcsServiceMessageNames.ClusterNameAttribute].Should().Be("test-cluster");
        message.Properties[AwsEcsServiceMessageNames.ClusterRegionAttribute].Should().Be("ap-southeast-2");
        message.Properties[AwsEcsServiceMessageNames.UseInstanceRole].Should().Be("False");
    }

    [Test]
    public void WriteTargetCreationServiceMessage_WithWorkerAuth_SetsUseInstanceRoleToTrue()
    {
        // Act
        writer.WriteTargetCreationServiceMessage("ap-southeast-2",
            testCluster,
            workerAuth,
            scope,
            matchResult);
        var serviceMessages = log.Messages.GetServiceMessagesOfType(AwsEcsServiceMessageNames.CreateTargetName);

        // Assert
        using var _ = new AssertionScope();
        serviceMessages.Length.Should().Be(1);
        var message = serviceMessages[0];
        message.Properties[AwsEcsServiceMessageNames.UseInstanceRole].Should().Be("True");
    }

    [Test]
    public void WriteTargetCreationServiceMessage_WithAssumedRole_SetsAssumedRoleVariables()
    {
        // Act
        writer.WriteTargetCreationServiceMessage("ap-southeast-2",
            testCluster,
            oidcAuth,
            scope,
            matchResult);
        var serviceMessages = log.Messages.GetServiceMessagesOfType(AwsEcsServiceMessageNames.CreateTargetName);
        
        // Assert
        using var _ = new AssertionScope();
        serviceMessages.Length.Should().Be(1);
        var message = serviceMessages[0];
        
        message.Properties[AwsEcsServiceMessageNames.AssumeRole].Should().Be("True");
        message.Properties[AwsEcsServiceMessageNames.AssumeRoleArn].Should().Be("arn:aws:iam::123456789012:role/TestRole");
        message.Properties[AwsEcsServiceMessageNames.AssumeRoleSession].Should().Be("TestSessionName");
        message.Properties[AwsEcsServiceMessageNames.AssumeRoleSessionDurationSeconds].Should().Be("3600");
        message.Properties[AwsEcsServiceMessageNames.AssumeRoleExternalId].Should().Be("externalId");
        
        

    }
}