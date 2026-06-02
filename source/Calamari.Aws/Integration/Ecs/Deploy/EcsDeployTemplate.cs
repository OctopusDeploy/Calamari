using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Calamari.Aws.Inputs.Ecs;
using Octopus.Calamari.Contracts.Aws.Ecs;

namespace Calamari.Aws.Integration.Ecs.Deploy;

// Strongly-typed CloudFormation template builder for an ECS Fargate service deploy.
// Composes a `Cfn.Template` graph from `DeployEcsCommandInputs` + the parameter list,
// delegating per-shape mapping to the `Inputs.Ecs.*` extension methods.
sealed class EcsDeployTemplate
{
    const string FargateLaunchType = "FARGATE";
    const string AwsVpcNetworkMode = "awsvpc";
    const string LinuxOperatingSystemFamily = "LINUX";
    const string DefaultTaskExecutionPolicyArn = "arn:aws:iam::aws:policy/service-role/AmazonECSTaskExecutionRolePolicy";
    const string TaskExecutionPolicyArnParameterName = "AmazonECSTaskExecutionRolePolicyArn";

    readonly DeployEcsCommandInputs commandInputs;
    readonly IReadOnlyList<IEcsTemplateParameter> parameters;
    readonly HashSet<string> registeredParameterNames;
    readonly bool createsInTemplateExecutionRole;

    public EcsDeployTemplate(DeployEcsCommandInputs commandInputs, IReadOnlyList<IEcsTemplateParameter> parameters)
    {
        this.commandInputs = commandInputs;
        this.parameters = parameters;
        registeredParameterNames = parameters.Select(p => p.Name).ToHashSet();
        // No user-supplied execution role → template creates one in-stack and adds an
        // extra CFN parameter for the managed-policy ARN.
        createsInTemplateExecutionRole = !registeredParameterNames.Contains(EcsTemplateParameterNames.TaskExecutionRole);
    }

    public Cfn.Template Build() => new()
    {
        Parameters = BuildParametersSection(),
        Resources = BuildResourcesSection()
    };

    Dictionary<string, Cfn.ParameterDef> BuildParametersSection()
    {
        var section = parameters.ToDictionary(
            p => p.Name,
            p => new Cfn.ParameterDef { Type = p.CfnType, Default = p.Default });

        if (createsInTemplateExecutionRole)
        {
            section[TaskExecutionPolicyArnParameterName] = new Cfn.ParameterDef
            {
                Type = "String",
                Default = DefaultTaskExecutionPolicyArn
            };
        }

        return section;
    }

    Dictionary<string, Cfn.Resource> BuildResourcesSection()
    {
        var section = new Dictionary<string, Cfn.Resource>();

        if (createsInTemplateExecutionRole)
        {
            section[commandInputs.FallbackTaskExecutionRoleName] = Cfn.Resource.IamRole(BuildExecutionRoleProperties());
        }

        if (commandInputs.RequiresLogGroup)
        {
            section[commandInputs.LogGroupName] = Cfn.Resource.LogGroup(new Cfn.LogGroupProperties
            {
                LogGroupName = new Cfn.Ref(EcsTemplateParameterNames.LogGroupName)
            });
        }

        section[commandInputs.TaskName] = Cfn.Resource.TaskDefinition(BuildTaskDefinitionProperties());
        section[commandInputs.ServiceName] = Cfn.Resource.Service(commandInputs.TaskName, BuildServiceProperties());

        return section;
    }

    Cfn.IamRoleProperties BuildExecutionRoleProperties() => new()
    {
        Path = "/",
        ManagedPolicyArns = [new Cfn.Ref(TaskExecutionPolicyArnParameterName)],
        AssumeRolePolicyDocument = new Cfn.AssumeRolePolicyDocument
        {
            Version = "2012-10-17",
            Statement =
            [
                new Cfn.AssumeRoleStatement
                {
                    Effect = "Allow",
                    Principal = new Cfn.AssumeRolePrincipal { Service = ["ecs-tasks.amazonaws.com"] },
                    Action = ["sts:AssumeRole"]
                }
            ]
        }
    };

    Cfn.TaskDefinitionProperties BuildTaskDefinitionProperties()
    {
        // For Auto-logging containers we point awslogs at the LogGroupName parameter
        // and the deploy region. LogGroupName is only registered when any container is Auto
        // (RequiresLogGroup), so the Ref is only valid in that case — null is fine because
        // ParseLogConfiguration only consults it in the Auto path.
        Cfn.Value<string> logGroupNameRef = commandInputs.RequiresLogGroup
            ? new Cfn.Ref(EcsTemplateParameterNames.LogGroupName)
            : null;
        Cfn.Value<string> awsRegionRef = new Cfn.Ref("AWS::Region");

        Cfn.Value<string> executionRoleArn = createsInTemplateExecutionRole
            ? new Cfn.Ref(commandInputs.FallbackTaskExecutionRoleName)
            : new Cfn.Ref(EcsTemplateParameterNames.TaskExecutionRole);

        return new Cfn.TaskDefinitionProperties
        {
            ContainerDefinitions = commandInputs.Containers.Select(c => BuildContainerDefinition(commandInputs, c, logGroupNameRef, awsRegionRef)).ToArray(),
            Family = new Cfn.Ref(EcsTemplateParameterNames.TaskDefinitionName),
            Cpu = new Cfn.Ref(EcsTemplateParameterNames.TaskDefinitionCpu),
            Memory = new Cfn.Ref(EcsTemplateParameterNames.TaskDefinitionMemory),
            ExecutionRoleArn = executionRoleArn,
            TaskRoleArn = StringRefOr(EcsTemplateParameterNames.TaskRole, commandInputs.TaskRole),
            RequiresCompatibilities = [FargateLaunchType],
            NetworkMode = AwsVpcNetworkMode,
            RuntimePlatform = new Cfn.RuntimePlatform
            {
                OperatingSystemFamily = LinuxOperatingSystemFamily,
                CpuArchitecture = commandInputs.CpuArchitecture
            },
            Volumes = commandInputs.Volumes.ParseVolumes(),
            Tags = commandInputs.Tags.ToCloudFormationTags()
        };
    }

    Cfn.ServiceProperties BuildServiceProperties() => new()
    {
        Cluster = new Cfn.Ref(EcsTemplateParameterNames.ClusterName),
        LaunchType = FargateLaunchType,
        TaskDefinition = new Cfn.Ref(commandInputs.TaskName),
        DesiredCount = NumberRefOr(EcsTemplateParameterNames.DesiredCount, commandInputs.DesiredCount),
        EnableEcsManagedTags = commandInputs.EnableEcsManagedTags,
        DeploymentConfiguration = new Cfn.DeploymentConfiguration
        {
            MinimumHealthyPercent = NumberRefOr(EcsTemplateParameterNames.MinimumHealthPercent, commandInputs.MinimumHealthyPercentage),
            MaximumPercent = NumberRefOr(EcsTemplateParameterNames.MaximumHealthPercent, commandInputs.MaximumHealthyPercentage)
        },
        NetworkConfiguration = new Cfn.NetworkConfiguration
        {
            AwsvpcConfiguration = new Cfn.AwsvpcConfiguration
            {
                AssignPublicIp = commandInputs.AutoAssignPublicIp,
                Subnets = commandInputs.SubnetIDs,
                SecurityGroups = commandInputs.NetworkSecurityGroupIds
            }
        },
        LoadBalancers = commandInputs.LoadBalancerMappings.ToLoadBalancerProperties(),
        Tags = commandInputs.Tags.ToCloudFormationTags()
    };

    static Cfn.ContainerDefinition BuildContainerDefinition(
        DeployEcsCommandInputs commandInputs,
        ContainerSpec c,
        Cfn.Value<string> logGroupNameRef,
        Cfn.Value<string> awsRegionRef) => new()
    {
        Name = c.ContainerName,
        Image = commandInputs.ResolveImageName(c.ContainerImageReference),
        Essential = c.Essential.ConvertedOrDefault(bool.Parse),
        DisableNetworking = c.NetworkSettings.DisableNetworking.ConvertedOrDefault(bool.Parse),
        WorkingDirectory = string.IsNullOrEmpty(c.WorkingDirectory) ? null : c.WorkingDirectory,
        Memory = c.MemoryLimitHard.ConvertedOrDefault<int?>(s => int.Parse(s, CultureInfo.InvariantCulture)),
        MemoryReservation = c.MemoryLimitSoft.ConvertedOrDefault<int?>(s => int.Parse(s, CultureInfo.InvariantCulture)),
        Cpu = c.Cpus.ConvertedOrDefault<int?>(s => int.Parse(s, CultureInfo.InvariantCulture)),
        User = string.IsNullOrEmpty(c.User) ? null : c.User,
        StartTimeout = c.StartTimeout.ConvertedOrDefault<double?>(s => double.Parse(s, CultureInfo.InvariantCulture)),
        StopTimeout = c.StopTimeout.ConvertedOrDefault<double?>(s => double.Parse(s, CultureInfo.InvariantCulture)),
        // SPF always emits these arrays even when empty — preserve that shape.
        DnsServers = c.NetworkSettings.DnsServers.ToArray(),
        DnsSearchDomains = c.NetworkSettings.DnsSearchDomains.ToArray(),
        ReadonlyRootFilesystem = c.ContainerStorage.ReadOnlyRootFileSystem.ConvertedOrDefault(bool.Parse),
        Command = c.Command.ConvertedOrDefault<string[]>(s => [s], () => null),
        EntryPoint = c.EntryPoint.ConvertedOrDefault<string[]>(s => s.Split(',').Select(x => x.Trim()).ToArray(), () => null),
        ResourceRequirements = c.ParseResourceRequirements(),
        DockerLabels = c.ParseDockerLabels(),
        PortMappings = c.ParsePortMappings(),
        HealthCheck = c.ParseHealthCheck(),
        ExtraHosts = c.ParseExtraHosts(),
        RepositoryCredentials = c.ParseRepositoryCredentials(),
        Ulimits = c.ParseULimits(),
        MountPoints = c.ParseMountPoints(),
        DependsOn = c.ParseDependencies(),
        VolumesFrom = c.ParseVolumesFrom(),
        LogConfiguration = c.ParseLogConfiguration(logGroupNameRef, awsRegionRef),
        EnvironmentFiles = c.ParseEnvironmentFiles(),
        FirelensConfiguration = c.ParseFireLensConfiguration(),
        Environment = c.ParseEnvironmentVariables(),
        Secrets = c.ParseSecrets()
    };

    // Conditionally-registered parameters: when the parameter exists, emit a Ref so CFN
    // parameter overrides at deploy time take effect; otherwise inline the literal.
    Cfn.Value<string> StringRefOr(string parameterName, string literal) =>
        registeredParameterNames.Contains(parameterName) ? new Cfn.Ref(parameterName) : literal;

    Cfn.Value<int> NumberRefOr(string parameterName, int literal) =>
        registeredParameterNames.Contains(parameterName) ? new Cfn.Ref(parameterName) : literal;
}