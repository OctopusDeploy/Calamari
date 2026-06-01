using System;
using System.Collections.Generic;
using System.Linq;
using Amazon.CDK;
using Amazon.CDK.AWS.ECS;
using Amazon.CDK.AWS.Logs;
using Calamari.Aws.Inputs.Ecs;

namespace Calamari.Aws.Integration.Ecs.Deploy;

public sealed class EcsDeployTemplate : Stack
{
    const string FargateLaunchType = "FARGATE";
    const string AwsVpcNetworkMode = "awsvpc";
    const string LinuxOperatingSystemFamily = "LINUX";

    readonly Dictionary<string, CfnParameter> paramRefs;

    public EcsDeployTemplate(DeployEcsCommandInputs commandInputs,
                             IReadOnlyList<IEcsTemplateParameter> parameters,
                             App scope,
                             string id,
                             IStackProps props = null) : base(scope, id, props)
    {
        TemplateOptions.TemplateFormatVersion = "2010-09-09";

        paramRefs = parameters.ToDictionary(
                                            p => p.Name,
                                            p => new CfnParameter(this,
                                                                  p.Name,
                                                                  new CfnParameterProps
                                                                  {
                                                                      Type = p.CfnType,
                                                                      Default = p.Default
                                                                  }));

        // ExecutionRoleArn: parameter when user-supplied (in `paramRefs`), in-template
        // role otherwise. The role can't be known at request time because CFN creates
        // it during the same deploy, so it can't sit behind a parameter override.
        var executionRoleArnRef = paramRefs.TryGetValue(EcsTemplateParameterNames.TaskExecutionRole, out var execRoleParam)
            ? execRoleParam.ValueAsString
            : commandInputs.MapTaskExecutionRoleArn(this);

        // For Auto-logging containers we need to point awslogs at the LogGroupName parameter
        // and the deploy region. LogGroupName is only registered when any container is Auto
        // (RequiresLogGroup), so accessing it outside that branch would throw — null is fine
        // because ParseLogConfiguration only consults it in the Auto path.
        var logGroupNameRef = commandInputs.RequiresLogGroup
            ? paramRefs[EcsTemplateParameterNames.LogGroupName].ValueAsString
            : null;
        
        // Stack.Region is a CDK token that synthesises to { Ref: AWS::Region } —
        // the CFN pseudo-parameter that resolves to the deploy region at runtime.
        var awsRegionRef = Region;

        var containers = commandInputs.Containers.Select(c => new CfnTaskDefinition.ContainerDefinitionProperty
        {
            Name = c.ContainerName,
            Image = c.ContainerImageReference.ImageName,
            Essential = c.Essential.ConvertedOrDefault(bool.Parse),
            DisableNetworking = c.NetworkSettings.DisableNetworking.ConvertedOrDefault(bool.Parse),
            WorkingDirectory = c.WorkingDirectory,
            Memory = c.MemoryLimitHard.ConvertedOrDefault<double?>(s => double.Parse(s)),
            MemoryReservation = c.MemoryLimitSoft.ConvertedOrDefault<double?>(s => double.Parse(s)),
            Cpu =  c.Cpus.ConvertedOrDefault<double?>(s => int.Parse(s)),
            User = c.User,
            StartTimeout = c.StartTimeout.ConvertedOrDefault<double?>( s => double.Parse(s)),
            StopTimeout = c.StopTimeout.ConvertedOrDefault<double?>(s => double.Parse(s)),
            DnsServers = c.NetworkSettings.DnsServers.ToArray(),
            DnsSearchDomains = c.NetworkSettings.DnsSearchDomains.ToArray(),
            ReadonlyRootFilesystem = c.ContainerStorage.ReadOnlyRootFileSystem.ConvertedOrDefault(bool.Parse),
            
            Command = c.Command.ConvertedOrDefault<string[]>(s => [s], () => null),
            EntryPoint =  c.EntryPoint.ConvertedOrDefault<string[]>(input => input.Split(',').Select(s => s.Trim()).ToArray(), () => null),
            
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
            
            // SPF referenced these properties but never set them.
            // Due to TS vs. CS SDK differences, we don't even mention them,
            // so they won't appear in the final template at all.
            // They appear here for consistency
            // Privileged = null, 
            // Links = null, 
            // DockerSecurityOptions = null 
            
        }).ToArray();

        if (commandInputs.RequiresLogGroup)
        {
            _ = new CfnLogGroup(this,
                               commandInputs.LogGroupName,
                               new CfnLogGroupProps
                               {
                                   LogGroupName = paramRefs[EcsTemplateParameterNames.LogGroupName].ValueAsString
                               });
        }

        var taskDefinition = new CfnTaskDefinition(this,
                                                   commandInputs.TaskName,
                                                   new CfnTaskDefinitionProps
                                                   {
                                                       ContainerDefinitions = containers,
                                                       Family = paramRefs[EcsTemplateParameterNames.TaskDefinitionName].ValueAsString,
                                                       Cpu = paramRefs[EcsTemplateParameterNames.TaskDefinitionCpu].ValueAsString,
                                                       Memory = paramRefs[EcsTemplateParameterNames.TaskDefinitionMemory].ValueAsString,
                                                       ExecutionRoleArn = executionRoleArnRef,
                                                       TaskRoleArn = ParamOr(EcsTemplateParameterNames.TaskRole, commandInputs.TaskRole),
                                                       RequiresCompatibilities = [FargateLaunchType],
                                                       NetworkMode = AwsVpcNetworkMode,
                                                       RuntimePlatform = new CfnTaskDefinition.RuntimePlatformProperty
                                                       {
                                                           OperatingSystemFamily = LinuxOperatingSystemFamily,
                                                           CpuArchitecture = commandInputs.CpuArchitecture
                                                       },
                                                       Volumes = commandInputs.Volumes.ParseVolumes(),
                                                       Tags = commandInputs.Tags.ToCloudFormationTags()
                                                   });

        var service = new CfnService(this,
                                     commandInputs.ServiceName,
                                     new CfnServiceProps
                                     {
                                         Cluster = paramRefs[EcsTemplateParameterNames.ClusterName].ValueAsString,
                                         LaunchType = FargateLaunchType,
                                         TaskDefinition = taskDefinition.Ref,
                                         DesiredCount = ParamOr(EcsTemplateParameterNames.DesiredCount, commandInputs.DesiredCount),
                                         EnableEcsManagedTags = commandInputs.EnableEcsManagedTags,
                                         DeploymentConfiguration = new CfnService.DeploymentConfigurationProperty
                                         {
                                             MinimumHealthyPercent = ParamOr(EcsTemplateParameterNames.MinimumHealthPercent, commandInputs.MinimumHealthyPercentage),
                                             MaximumPercent = ParamOr(EcsTemplateParameterNames.MaximumHealthPercent, commandInputs.MaximumHealthyPercentage)
                                         },
                                         NetworkConfiguration = new CfnService.NetworkConfigurationProperty
                                         {
                                             AwsvpcConfiguration = new CfnService.AwsVpcConfigurationProperty
                                             {
                                                 AssignPublicIp = commandInputs.AutoAssignPublicIp,
                                                 Subnets = commandInputs.SubnetIDs,
                                                 SecurityGroups = commandInputs.NetworkSecurityGroupIds
                                             }
                                         },
                                         LoadBalancers = commandInputs.LoadBalancerMappings.ToLoadBalancerProperties(),
                                         Tags = commandInputs.Tags.ToCloudFormationTags(),
                                     });
        
        service.AddDependency(taskDefinition);
    }

    // Conditionally-registered parameters (only present when the input was customised
    // away from the default): fall back to the literal commandInputs value when absent.
    // Resources then render either { Ref: ... } or the inline value accordingly.
    string ParamOr(string key, string literal) =>
        paramRefs.TryGetValue(key, out var p) ? p.ValueAsString : literal;

    double ParamOr(string key, double literal) =>
        paramRefs.TryGetValue(key, out var p) ? p.ValueAsNumber : literal;
}
