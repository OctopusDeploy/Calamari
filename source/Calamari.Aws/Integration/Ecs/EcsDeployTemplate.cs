using System.Collections.Generic;
using System.Linq;
using Amazon.CDK;
using Amazon.CDK.AWS.ECS;
using Amazon.CDK.AWS.Logs;
using Calamari.Aws.Inputs.Ecs;
using Octopus.Calamari.Contracts.Aws.Ecs;

namespace Calamari.Aws.Integration.Ecs;

public sealed class EcsDeployTemplate : Stack
{
    const string FargateLaunchType = "FARGATE";
    const string AwsVpcNetworkMode = "awsvpc";
    const string LinuxOperatingSystemFamily = "LINUX";
    

    public EcsDeployTemplate(DeployEcsCommandInputs commandInputs,
                             IReadOnlyList<(string Name, string Value)> parameters,
                             App scope,
                             string id,
                             IStackProps props = null) : base(scope, id, props)
    {
        TemplateOptions.TemplateFormatVersion = "2010-09-09";

        var paramRefs = parameters.ToDictionary(
                                                p => p.Name,
                                                p => new CfnParameter(this,
                                                                      p.Name,
                                                                      new CfnParameterProps
                                                                      {
                                                                          Type = "String",
                                                                          Default = p.Value
                                                                      }));

        // ExecutionRoleArn: parameter when user-supplied (in `paramRefs`), in-template
        // role otherwise. The role can't be known at request time because CFN creates
        // it during the same deploy, so it can't sit behind a parameter override.
        var executionRoleArnRef = paramRefs.TryGetValue(EcsTemplateParameterNames.TaskExecutionRole, out var execRoleParam)
            ? execRoleParam.ValueAsString
            : commandInputs.MapTaskExecutionRoleArn(this);


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
            LogConfiguration = c.ParseLogConfiguration(),
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
                                                       TaskRoleArn = paramRefs[EcsTemplateParameterNames.TaskRole].ValueAsString,
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
                                         DesiredCount = commandInputs.DesiredCount,
                                         EnableEcsManagedTags = commandInputs.EnableEcsManagedTags,
                                         DeploymentConfiguration = new CfnService.DeploymentConfigurationProperty
                                         {
                                             MinimumHealthyPercent = commandInputs.MinimumHealthyPercentage,
                                             MaximumPercent = commandInputs.MaximumHealthyPercentage
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
    
}
