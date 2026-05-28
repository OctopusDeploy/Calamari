using System;
using System.Linq;
using Amazon.CDK;
using Amazon.CDK.AWS.ECS;
using Calamari.Aws.Inputs.Ecs;

namespace Calamari.Aws.Integration.Ecs;

public sealed class EcsDeployTemplate : Stack
{
    const string FargateLaunchType = "FARGATE";
    const string AwsVpcNetworkMode = "awsvpc";
    const string LinuxOperatingSystemFamily = "LINUX";
    

    public EcsDeployTemplate(DeployEcsCommandInputs commandInputs, App scope, string id, IStackProps props = null) : base(scope, id, props)
    {
        TemplateOptions.TemplateFormatVersion = "2010-09-09";

        var clusterNameParam = new CfnParameter(this,
                                                "ClusterName",
                                                new CfnParameterProps
                                                {
                                                    Type = "String",
                                                    Default =  commandInputs.ClusterName
                                                });

        var taskFamilyParam = new CfnParameter(this,
                                               "TaskDefinitionName",
                                               new CfnParameterProps
                                               {
                                                   Type = "String",
                                                   Default = commandInputs.ServiceTaskName
                                               });

        var cpuParam = new CfnParameter(this,
                                        "TaskDefinitionCPU",
                                        new CfnParameterProps
                                        {
                                            Type = "String",
                                            Default = commandInputs.Cpu
                                        });

        var memoryParam = new CfnParameter(this,
                                           "TaskDefinitionMemory",
                                           new CfnParameterProps
                                           {
                                               Type = "String",
                                               Default = commandInputs.Memory
                                           });

        var executionRoleArnParam = new CfnParameter(this,
                                                     "TaskExecutionRole",
                                                     new CfnParameterProps
                                                     {
                                                         Type = "String",
                                                         Default = commandInputs.MapTaskExecutionRoleArn(this)
                                                     });

        var taskRoleArnParam = new CfnParameter(this,
                                                "TaskRole",
                                                new CfnParameterProps
                                                {
                                                    Type = "String",
                                                    Default = commandInputs.TaskRole
                                                });

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
            EntryPoint =  c.EntryPoint.ConvertedOrDefault<string[]>(s => [s], () => null),
            
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
            Secrets = c.ParseSecrets(),
            
            
            
            // SPF referenced these properties but never set them.
            // Due to TS vs. CS SDK differences, we don't even mention them,
            // so they won't appear in the final template at all.
            // They appear here for consistency
            // Privileged = null, 
            // Links = null, 
            // DockerSecurityOptions = null 
            
        }).ToArray();

        var taskDefinition = new CfnTaskDefinition(this,
                                                   commandInputs.TaskName,
                                                   new CfnTaskDefinitionProps
                                                   {
                                                       ContainerDefinitions = containers,
                                                       Family = taskFamilyParam.ValueAsString,
                                                       Cpu = cpuParam.ValueAsString,
                                                       Memory = memoryParam.ValueAsString,
                                                       ExecutionRoleArn = executionRoleArnParam.ValueAsString,
                                                       TaskRoleArn = taskRoleArnParam.ValueAsString,
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
                                         Cluster = clusterNameParam.ValueAsString,
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
