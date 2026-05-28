using System;
using System.Collections.Generic;
using Amazon.CDK.AWS.ECS;
using Calamari.Aws.Inputs.Ecs;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Calamari.Contracts.Aws.Ecs;
using ContainerDependency = Octopus.Calamari.Contracts.Aws.Ecs.ContainerDependency;
using ContainerDependencyCondition = Octopus.Calamari.Contracts.Aws.Ecs.ContainerDependencyCondition;
using ContainerMountPoint = Octopus.Calamari.Contracts.Aws.Ecs.ContainerMountPoint;
using HealthCheck = Octopus.Calamari.Contracts.Aws.Ecs.HealthCheck;
using LogDriver = Octopus.Calamari.Contracts.Aws.Ecs.LogDriver;
using Ulimit = Octopus.Calamari.Contracts.Aws.Ecs.Ulimit;

namespace Calamari.Tests.AWS.Inputs.Ecs;

[TestFixture]
public class ContainerSpecMappingExtensionsTests
{
    [Test]
    public void ParseMountPoints_WhenNoMountPoints_ReturnsNull()
    {
        var spec = new ContainerSpec();

        var result = spec.ParseMountPoints();

        result.Should().BeNull();
    }

    [Test]
    public void ParseMountPoints_WithMountPoints_MapsAllProperties()
    {
        var spec = new ContainerSpec
        {
            ContainerStorage = new ContainerStorage
            {
                MountPoints =
                [
                    new ContainerMountPoint
                    {
                        SourceVolume = "my-volume",
                        ContainerPath = "/data",
                        Readonly = "true"
                    }
                ]
            }
        };

        var result = spec.ParseMountPoints();

        result.Should().HaveCount(1);
        result[0].SourceVolume.Should().Be("my-volume");
        result[0].ContainerPath.Should().Be("/data");
        result[0].ReadOnly.Should().Be(true);
    }

    [Test]
    public void ParseMountPoints_WithEmptyReadonly_DefaultsToFalse()
    {
        var spec = new ContainerSpec
        {
            ContainerStorage = new ContainerStorage
            {
                MountPoints =
                [
                    new ContainerMountPoint
                    {
                        SourceVolume = "v",
                        ContainerPath = "/p",
                        Readonly = string.Empty
                    }
                ]
            }
        };

        var result = spec.ParseMountPoints();

        result[0].ReadOnly.Should().Be(false);
    }

    [Test]
    public void ParseMountPoints_WithEmptySourceAndPath_ReturnsNulls()
    {
        var spec = new ContainerSpec
        {
            ContainerStorage = new ContainerStorage
            {
                MountPoints =
                [
                    new ContainerMountPoint
                    {
                        SourceVolume = string.Empty,
                        ContainerPath = string.Empty,
                        Readonly = "false"
                    }
                ]
            }
        };

        var result = spec.ParseMountPoints();

        result[0].SourceVolume.Should().BeNull();
        result[0].ContainerPath.Should().BeNull();
    }

    [Test]
    public void ParseDependencies_WhenNoDependencies_ReturnsNull()
    {
        var spec = new ContainerSpec();

        var result = spec.ParseDependencies();

        result.Should().BeNull();
    }

    [Test]
    [TestCase(ContainerDependencyCondition.Start, "START")]
    [TestCase(ContainerDependencyCondition.Complete, "COMPLETE")]
    [TestCase(ContainerDependencyCondition.Success, "SUCCESS")]
    [TestCase(ContainerDependencyCondition.Healthy, "HEALTHY")]
    public void ParseDependencies_MapsConditionToUpperInvariant(ContainerDependencyCondition condition, string expected)
    {
        var spec = new ContainerSpec
        {
            Dependencies =
            [
                new ContainerDependency
                {
                    ContainerName = "sidecar",
                    Condition = condition
                }
            ]
        };

        var result = spec.ParseDependencies();

        result.Should().HaveCount(1);
        result[0].ContainerName.Should().Be("sidecar");
        result[0].Condition.Should().Be(expected);
    }

    [Test]
    public void ParseDependencies_WithEmptyContainerName_ReturnsNull()
    {
        var spec = new ContainerSpec
        {
            Dependencies =
            [
                new ContainerDependency
                {
                    ContainerName = string.Empty,
                    Condition = ContainerDependencyCondition.Start
                }
            ]
        };

        var result = spec.ParseDependencies();

        result[0].ContainerName.Should().BeNull();
    }

    [Test]
    public void ParseDependencies_WithMultipleDependencies_PreservesOrder()
    {
        var spec = new ContainerSpec
        {
            Dependencies =
            [
                new ContainerDependency { ContainerName = "a", Condition = ContainerDependencyCondition.Start },
                new ContainerDependency { ContainerName = "b", Condition = ContainerDependencyCondition.Healthy }
            ]
        };

        var result = spec.ParseDependencies();

        result.Should().HaveCount(2);
        result[0].ContainerName.Should().Be("a");
        result[1].ContainerName.Should().Be("b");
    }

    [Test]
    public void ParseVolumesFrom_WhenNoVolumesFrom_ReturnsEmptyArray()
    {
        var spec = new ContainerSpec();

        var result = spec.ParseVolumesFrom();

        result.Should().BeEmpty();
    }

    [Test]
    public void ParseVolumesFrom_WithVolumeFrom_MapsAllProperties()
    {
        var spec = new ContainerSpec
        {
            ContainerStorage = new ContainerStorage
            {
                VolumeFrom =
                [
                    new ContainerVolumeFrom
                    {
                        SourceContainer = "shared-container",
                        Readonly = "true"
                    }
                ]
            }
        };

        var result = spec.ParseVolumesFrom();

        result.Should().HaveCount(1);
        result[0].SourceContainer.Should().Be("shared-container");
        result[0].ReadOnly.Should().Be(true);
    }

    [Test]
    public void ParseVolumesFrom_WithEmptySourceContainer_ReturnsNull()
    {
        var spec = new ContainerSpec
        {
            ContainerStorage = new ContainerStorage
            {
                VolumeFrom =
                [
                    new ContainerVolumeFrom
                    {
                        SourceContainer = string.Empty,
                        Readonly = "false"
                    }
                ]
            }
        };

        var result = spec.ParseVolumesFrom();

        result[0].SourceContainer.Should().BeNull();
        result[0].ReadOnly.Should().Be(false);
    }

    [Test]
    public void ParseVolumesFrom_WithEmptyReadonly_DefaultsToFalse()
    {
        var spec = new ContainerSpec
        {
            ContainerStorage = new ContainerStorage
            {
                VolumeFrom =
                [
                    new ContainerVolumeFrom
                    {
                        SourceContainer = "c",
                        Readonly = string.Empty
                    }
                ]
            }
        };

        var result = spec.ParseVolumesFrom();

        result[0].ReadOnly.Should().Be(false);
    }

    [Test]
    public void ParseEnvironmentVariables_WhenNone_ReturnsEmptyDictionary()
    {
        var spec = new ContainerSpec();

        var result = spec.ParseEnvironmentVariables();

        result.Should().BeEmpty();
    }

    [Test]
    public void ParseEnvironmentVariables_MapsKeysToValues()
    {
        var spec = new ContainerSpec
        {
            EnvironmentVariables =
            [
                new TypedKeyValuePair { Type = KeyValueType.Plain, Key = "LOG_LEVEL", Value = "INFO" },
                new TypedKeyValuePair { Type = KeyValueType.Plain, Key = "REGION", Value = "us-east-1" }
            ]
        };

        var result = spec.ParseEnvironmentVariables();

        result.Should().HaveCount(2);
        result.Should().Contain(x => x.Name == "LOG_LEVEL" && x.Value == "INFO");
        result.Should().Contain(x => x.Name == "REGION" && x.Value == "us-east-1");
    }

    [Test]
    public void ParseEnvironmentVariables_WithDuplicateKeys_LastValueWins()
    {
        var spec = new ContainerSpec
        {
            EnvironmentVariables =
            [
                new TypedKeyValuePair { Type = KeyValueType.Plain, Key = "LOG_LEVEL", Value = "DEBUG" },
                new TypedKeyValuePair { Type = KeyValueType.Plain, Key = "LOG_LEVEL", Value = "INFO" },
                new TypedKeyValuePair { Type = KeyValueType.Plain, Key = "REGION", Value = "us-east-1" }
            ]
        };

        var result = spec.ParseEnvironmentVariables();

        result.Should().HaveCount(2);
        result.Should().Contain(x => x.Name == "LOG_LEVEL" && x.Value == "INFO");
        result.Should().Contain(x => x.Name == "REGION" && x.Value == "us-east-1");
    }

    [Test]
    public void ParseEnvironmentVariables_ExcludesSecretEntries()
    {
        var spec = new ContainerSpec
        {
            EnvironmentVariables =
            [
                new TypedKeyValuePair { Type = KeyValueType.Plain, Key = "PLAIN_KEY", Value = "plain-value" },
                new TypedKeyValuePair { Type = KeyValueType.Secret, Key = "SECRET_KEY", Value = "arn:secret" }
            ]
        };

        var result = spec.ParseEnvironmentVariables();

        result.Should().HaveCount(1);
        result.Should().Contain(x => x.Name == "PLAIN_KEY" && x.Value == "plain-value");
        result.Should().NotContain(x => x.Name =="SECRET_KEY");
    }

    [Test]
    public void ParseEnvironmentVariables_DedupeAppliesAfterFilteringSecrets()
    {
        // A Secret entry with the same key as a Plain entry must not displace the Plain value.
        var spec = new ContainerSpec
        {
            EnvironmentVariables =
            [
                new TypedKeyValuePair { Type = KeyValueType.Plain, Key = "TOKEN", Value = "plain-token" },
                new TypedKeyValuePair { Type = KeyValueType.Secret, Key = "TOKEN", Value = "arn:secret" }
            ]
        };

        var result = spec.ParseEnvironmentVariables();

        result.Should().HaveCount(1);
        result.Should().Contain(x => x.Name == "TOKEN" && x.Value == "plain-token");
    }

    [Test]
    public void ParseSecrets_WhenNone_ReturnsNull()
    {
        var spec = new ContainerSpec();

        var result = spec.ParseSecrets();

        result.Should().BeNull();
    }

    [Test]
    public void ParseSecrets_OnlyIncludesSecretTypedEntries()
    {
        var spec = new ContainerSpec
        {
            EnvironmentVariables =
            [
                new TypedKeyValuePair { Type = KeyValueType.Plain, Key = "PLAIN_KEY", Value = "plain-value" },
                new TypedKeyValuePair { Type = KeyValueType.Secret, Key = "SECRET_KEY", Value = "arn:secret" }
            ]
        };

        var result = spec.ParseSecrets();

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("SECRET_KEY");
        result[0].ValueFrom.Should().Be("arn:secret");
    }

    [Test]
    public void ParseSecrets_WithDuplicateSecretKeys_LastValueWins()
    {
        var spec = new ContainerSpec
        {
            EnvironmentVariables =
            [
                new TypedKeyValuePair { Type = KeyValueType.Secret, Key = "TOKEN", Value = "arn:first" },
                new TypedKeyValuePair { Type = KeyValueType.Secret, Key = "TOKEN", Value = "arn:second" }
            ]
        };

        var result = spec.ParseSecrets();

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("TOKEN");
        result[0].ValueFrom.Should().Be("arn:second");
    }

    [Test]
    public void ParseSecrets_DoesNotConsiderPlainEntriesForDedupe()
    {
        // A Plain entry with the same key as a Secret must not displace or merge with the Secret value.
        var spec = new ContainerSpec
        {
            EnvironmentVariables =
            [
                new TypedKeyValuePair { Type = KeyValueType.Plain, Key = "TOKEN", Value = "plain-token" },
                new TypedKeyValuePair { Type = KeyValueType.Secret, Key = "TOKEN", Value = "arn:secret" }
            ]
        };

        var result = spec.ParseSecrets();

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("TOKEN");
        result[0].ValueFrom.Should().Be("arn:secret");
    }

    [Test]
    public void ParseDockerLabels_WithDuplicateKeys_LastValueWins()
    {
        var spec = new ContainerSpec
        {
            DockerLabels =
            [
                new KeyValuePair<string, string>("env", "dev"),
                new KeyValuePair<string, string>("env", "prod"),
                new KeyValuePair<string, string>("owner", "team-a")
            ]
        };

        var result = spec.ParseDockerLabels();

        result.Should().HaveCount(2);
        result["env"].Should().Be("prod");
        result["owner"].Should().Be("team-a");
    }

    [Test]
    public void ParseHealthCheck_WhenCommandEmpty_ReturnsNull()
    {
        var spec = new ContainerSpec();

        var result = spec.ParseHealthCheck();

        result.Should().BeNull();
    }

    [Test]
    public void ParseHealthCheck_WhenCommandPresent_MapsAllProperties()
    {
        var spec = new ContainerSpec
        {
            HealthCheck = new HealthCheck
            {
                Command = ["CMD-SHELL", "curl -f http://localhost"],
                Interval = "30",
                Retries = "3",
                StartPeriod = "10",
                Timeout = "5"
            }
        };

        var result = spec.ParseHealthCheck();

        result.Should().NotBeNull();
        result!.Command.Should().BeEquivalentTo("CMD-SHELL", "curl -f http://localhost");
        result.Interval.Should().Be(30);
        result.Retries.Should().Be(3);
        result.StartPeriod.Should().Be(10);
        result.Timeout.Should().Be(5);
    }

    [Test]
    public void ParsePortMappings_MapsContainerPortAndProtocol()
    {
        var spec = new ContainerSpec
        {
            ContainerPortMappings =
            [
                new ContainerPortMapping { ContainerPort = "8080", Protocol = PortProtocol.Tcp }
            ]
        };

        var result = spec.ParsePortMappings();

        result.Should().HaveCount(1);
        result[0].ContainerPort.Should().Be(8080);
        result[0].HostPort.Should().Be(8080);
        result[0].Protocol.Should().Be("Tcp");
    }

    [Test]
    public void ParseRepositoryCredentials_WhenDefault_ReturnsNull()
    {
        var spec = new ContainerSpec
        {
            RepositoryAuthentication = new RepositoryAuthentication { Type = RepositoryAuthenticationType.Default }
        };

        var result = spec.ParseRepositoryCredentials();

        result.Should().BeNull();
    }

    [Test]
    public void ParseRepositoryCredentials_WhenSecretsManager_PopulatesCredentialsParameter()
    {
        var spec = new ContainerSpec
        {
            RepositoryAuthentication = new RepositoryAuthentication
            {
                Type = RepositoryAuthenticationType.SecretsManager,
                SecretName = "arn:aws:secretsmanager:us-east-1:123:secret:foo"
            }
        };

        var result = spec.ParseRepositoryCredentials();

        result.CredentialsParameter.Should().Be("arn:aws:secretsmanager:us-east-1:123:secret:foo");
    }

    [Test]
    public void ParseResourceRequirements_WhenGpusEmpty_ReturnsEmptyArray()
    {
        var spec = new ContainerSpec { Gpus = string.Empty };

        var result = spec.ParseResourceRequirements();

        result.Should().BeEmpty();
    }

    [Test]
    public void ParseResourceRequirements_WhenGpusPresent_ReturnsGpuRequirement()
    {
        var spec = new ContainerSpec { Gpus = "1" };

        var result = spec.ParseResourceRequirements();

        result.Should().HaveCount(1);
    }

    [Test]
    public void ParseULimits_WhenNone_ReturnsNull()
    {
        var spec = new ContainerSpec();

        var result = spec.ParseULimits();

        result.Should().BeNull();
    }

    [Test]
    public void ParseULimits_MapsLimitNameAndValues()
    {
        var spec = new ContainerSpec
        {
            Ulimits =
            [
                new Ulimit { LimitName = "nofile", HardLimit = "65536", SoftLimit = "1024" }
            ]
        };

        var result = spec.ParseULimits();

        result.Should().HaveCount(1);
        result[0].HardLimit.Should().Be(65536);
        result[0].SoftLimit.Should().Be(1024);
    }

    [Test]
    public void ParseExtraHosts_MapsHostnameAndIp()
    {
        var spec = new ContainerSpec
        {
            NetworkSettings = new ContainerNetworkSettings
            {
                ExtraHosts =
                [
                    new ExtraHost { Hostname = "example.local", IpAddress = "10.0.0.1" }
                ]
            }
        };

        var result = spec.ParseExtraHosts();

        result.Should().HaveCount(1);
        result[0].Hostname.Should().Be("example.local");
        result[0].IpAddress.Should().Be("10.0.0.1");
    }

    [Test]
    public void ConvertedOrDefault_WhenEmpty_ReturnsDefault()
    {
        var result = string.Empty.ConvertedOrDefault(int.Parse);

        result.Should().Be(0);
    }

    [Test]
    public void ConvertedOrDefault_WhenNonEmpty_AppliesConverter()
    {
        var result = "42".ConvertedOrDefault(int.Parse);

        result.Should().Be(42);
    }

    [Test]
    public void ConvertedOrDefault_WhenEmptyWithDefaultOverride_UsesOverride()
    {
        var result = string.Empty.ConvertedOrDefault(int.Parse, () => 99);

        result.Should().Be(99);
    }

    [Test]
    public void ConvertedOrDefault_WhenNonEmptyWithDefaultOverride_StillUsesConverter()
    {
        var result = "42".ConvertedOrDefault(int.Parse, () => 99);

        result.Should().Be(42);
    }

    [Test]
    public void ParseEnvironmentFiles_WhenNone_ReturnsEmptyArray()
    {
        var spec = new ContainerSpec();

        var result = spec.ParseEnvironmentFiles();

        result.Should().BeEmpty();
    }

    [Test]
    public void ParseEnvironmentFiles_WithFiles_MapsToS3TypeWithValue()
    {
        var spec = new ContainerSpec
        {
            EnvironmentFiles =
            [
                "arn:aws:s3:::my-bucket/env-one",
                "arn:aws:s3:::my-bucket/env-two"
            ]
        };

        var result = spec.ParseEnvironmentFiles();

        result.Should().HaveCount(2);
        result[0].Type.Should().Be("s3");
        result[0].Value.Should().Be("arn:aws:s3:::my-bucket/env-one");
        result[1].Type.Should().Be("s3");
        result[1].Value.Should().Be("arn:aws:s3:::my-bucket/env-two");
    }

    [Test]
    public void ParseLogConfiguration_WhenLogDriverNull_ReturnsNull()
    {
        var spec = new ContainerSpec
        {
            ContainerLogging = new ContainerLogging { LogDriver = null }
        };

        var result = spec.ParseLogConfiguration();

        result.Should().BeNull();
    }

    [Test]
    public void ParseLogConfiguration_WhenLogDriverNone_ReturnsNull()
    {
        var spec = new ContainerSpec
        {
            ContainerLogging = new ContainerLogging
            {
                LogDriver = LogDriver.None,
                Type = ContainerLoggingType.Manual
            }
        };

        var result = spec.ParseLogConfiguration();

        result.Should().BeNull();
    }

    [Test]
    public void ParseLogConfiguration_WhenAuto_ForcesAwsLogsDriver()
    {
        var spec = new ContainerSpec
        {
            ContainerLogging = new ContainerLogging
            {
                Type = ContainerLoggingType.Auto,
                LogDriver = LogDriver.Splunk
            }
        };

        var result = spec.ParseLogConfiguration();

        result.Should().NotBeNull();
        result!.LogDriver.Should().Be("awslogs");
    }

    [Test]
    public void ParseLogConfiguration_WhenManual_UsesProvidedLogDriver()
    {
        var spec = new ContainerSpec
        {
            ContainerLogging = new ContainerLogging
            {
                Type = ContainerLoggingType.Manual,
                LogDriver = LogDriver.Splunk
            }
        };

        var result = spec.ParseLogConfiguration();

        result!.LogDriver.Should().Be("splunk");
    }

    [Test]
    public void ParseLogConfiguration_SplitsPlainAndSecretOptions()
    {
        var spec = new ContainerSpec
        {
            ContainerLogging = new ContainerLogging
            {
                Type = ContainerLoggingType.Manual,
                LogDriver = LogDriver.AwsLogs,
                LogOptions =
                [
                    new TypedKeyValuePair { Type = KeyValueType.Plain, Key = "awslogs-region", Value = "us-east-1" },
                    new TypedKeyValuePair { Type = KeyValueType.Plain, Key = "awslogs-group", Value = "my-group" },
                    new TypedKeyValuePair { Type = KeyValueType.Secret, Key = "secret-token", Value = "arn:secret" }
                ]
            }
        };

        var result = spec.ParseLogConfiguration();

        result!.Options.Should().BeOfType<Dictionary<string, string>>()
              .Which.Should().BeEquivalentTo(new Dictionary<string, string>
              {
                  { "awslogs-region", "us-east-1" },
                  { "awslogs-group", "my-group" }
              });
        result.SecretOptions.Should().BeOfType<Dictionary<string, string>>()
              .Which.Should().BeEquivalentTo(new Dictionary<string, string>
              {
                  { "secret-token", "arn:secret" }
              });
    }

    [Test]
    public void ParseFireLensConfiguration_WhenDisabled_ReturnsNull()
    {
        var spec = new ContainerSpec
        {
            FirelensConfiguration = new ContainerFireLensConfiguration { Type = FireLensConfigurationType.Disabled }
        };

        var result = spec.ParseFireLensConfiguration();

        result.Should().BeNull();
    }

    [Test]
    public void ParseFireLensConfiguration_WhenDefaultSpec_ReturnsNull()
    {
        // FireLensConfigurationType.Disabled is the default enum value
        var spec = new ContainerSpec();

        var result = spec.ParseFireLensConfiguration();

        result.Should().BeNull();
    }

    [Test]
    [TestCase(FireLensType.Fluentd, "fluentd")]
    [TestCase(FireLensType.Fluentbit, "fluentbit")]
    public void ParseFireLensConfiguration_WhenEnabled_MapsTypeLowercase(FireLensType firelensType, string expected)
    {
        var spec = new ContainerSpec
        {
            FirelensConfiguration = new ContainerFireLensConfiguration
            {
                Type = FireLensConfigurationType.Enabled,
                FirelensType = firelensType,
                EnableEcsLogMetadata = "true"
            }
        };

        var result = spec.ParseFireLensConfiguration();

        result.Should().NotBeNull();
        result!.Type.Should().Be(expected);
    }

    [Test]
    public void ParseFireLensConfiguration_WhenEnabled_AlwaysIncludesEnableEcsLogMetadata()
    {
        var spec = new ContainerSpec
        {
            FirelensConfiguration = new ContainerFireLensConfiguration
            {
                Type = FireLensConfigurationType.Enabled,
                FirelensType = FireLensType.Fluentbit,
                EnableEcsLogMetadata = "false"
            }
        };

        var result = spec.ParseFireLensConfiguration();

        result!.Options.Should().BeOfType<Dictionary<string, string>>()
              .Which.Should().ContainKey("enable-ecs-log-metadata")
              .WhoseValue.Should().Be("false");
    }

    [Test]
    public void ParseFireLensConfiguration_WithCustomConfigSourceNone_OmitsConfigFileOptions()
    {
        var spec = new ContainerSpec
        {
            FirelensConfiguration = new ContainerFireLensConfiguration
            {
                Type = FireLensConfigurationType.Enabled,
                FirelensType = FireLensType.Fluentbit,
                EnableEcsLogMetadata = "true",
                CustomConfigSource = new FireLensCustomConfigSource { Type = FireLensCustomConfigSourceType.None }
            }
        };

        var result = spec.ParseFireLensConfiguration();

        var options = (Dictionary<string, string>)result!.Options;
        options.Should().NotContainKey("config-file-type");
        options.Should().NotContainKey("config-file-value");
    }

    [Test]
    [TestCase(FireLensCustomConfigSourceType.File, "file")]
    [TestCase(FireLensCustomConfigSourceType.S3, "s3")]
    public void ParseFireLensConfiguration_WithCustomConfigSource_AddsConfigFileOptions(FireLensCustomConfigSourceType sourceType, string expected)
    {
        var spec = new ContainerSpec
        {
            FirelensConfiguration = new ContainerFireLensConfiguration
            {
                Type = FireLensConfigurationType.Enabled,
                FirelensType = FireLensType.Fluentbit,
                EnableEcsLogMetadata = "true",
                CustomConfigSource = new FireLensCustomConfigSource
                {
                    Type = sourceType,
                    FilePath = "/etc/fluent.conf"
                }
            }
        };

        var result = spec.ParseFireLensConfiguration();

        var options = (Dictionary<string, string>)result!.Options;
        options.Should().Contain("config-file-type", expected);
        options.Should().Contain("config-file-value", "/etc/fluent.conf");
    }
}
