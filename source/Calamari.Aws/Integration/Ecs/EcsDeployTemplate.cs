using System;
using System.Linq;
using Amazon.CDK;
using Amazon.CDK.AWS.ECS;
using Amazon.CDK.AWS.IAM;
using Calamari.Aws.Inputs;


namespace Calamari.Aws.Integration.Ecs;

public sealed class EcsDeployTemplate : Stack
{
    const string FargateLaunchType = "FARGATE";
    const string AwsVpcNetworkMode = "awsvpc";
    const string LinuxOperatingSystemFamily = "LINUX";
    const string DefaultTaskExecutionPolicyArn = "arn:aws:iam::aws:policy/service-role/AmazonECSTaskExecutionRolePolicy";

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

        var executionRoleRef = ProcessTaskExecutionRole(commandInputs);

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
            
            Command = c.Command.ConvertedOrDefault<string[]>(s => [s], () => []),
            EntryPoint =  c.EntryPoint.ConvertedOrDefault<string[]>(s => [s], () => []),
            
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
            
            Privileged = false, // SPF never set value for this property, so we use default
            Links = [], // SPF never set value for this property
            DockerSecurityOptions = [] // SPF never set value for this property
            
        }).ToArray();

        var taskDefinition = new CfnTaskDefinition(this,
                                                   commandInputs.TaskName,
                                                   new CfnTaskDefinitionProps
                                                   {
                                                       ContainerDefinitions = containers,
                                                       Family = taskFamilyParam.ValueAsString,
                                                       Cpu = cpuParam.ValueAsString,
                                                       Memory = memoryParam.ValueAsString,
                                                       ExecutionRoleArn = executionRoleRef,
                                                       TaskRoleArn = string.IsNullOrEmpty(commandInputs.TaskRole) ? null : commandInputs.TaskRole,
                                                       RequiresCompatibilities = [FargateLaunchType],
                                                       NetworkMode = AwsVpcNetworkMode,
                                                       RuntimePlatform = new CfnTaskDefinition.RuntimePlatformProperty
                                                       {
                                                           OperatingSystemFamily = LinuxOperatingSystemFamily,
                                                           CpuArchitecture = commandInputs.CpuArchitecture
                                                       },
                                                       Volumes = Array.Empty<CfnTaskDefinition.VolumeProperty>(), // TODO: Read from variables
                                                       Tags = Array.Empty<CfnTag>() // TODO: Read From Varaibles
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
                                         LoadBalancers = null, // TODO: read from variables 
                                         Tags = Array.Empty<CfnTag>() // TODO: Read from Variables
                                     });
        
        // TODO: Add depdency on Load Balancer if require
        service.AddDependency(taskDefinition);
    }

    string ProcessTaskExecutionRole(DeployEcsCommandInputs inputs)
    {
        if (!string.IsNullOrEmpty(inputs.TaskExecutionRole))
        {
            return inputs.TaskExecutionRole;
        }

        var policyArnParam = new CfnParameter(this,
                                              "AmazonECSTaskExecutionRolePolicyArn",
                                              new CfnParameterProps
                                              {
                                                  Type = "String",
                                                  Default = DefaultTaskExecutionPolicyArn
                                              });

        var role = new CfnRole(this,
                               inputs.FallbackTaskExecutionRoleName,
                               new CfnRoleProps
                               {
                                   Path = "/",
                                   ManagedPolicyArns = [policyArnParam.ValueAsString],
                                   AssumeRolePolicyDocument = new PolicyDocument(new PolicyDocumentProps
                                   {
                                       Statements =
                                       [
                                           new PolicyStatement(new PolicyStatementProps
                                           {
                                               Effect = Effect.ALLOW,
                                               Principals = [new ServicePrincipal("ecs-tasks.amazonaws.com")],
                                               Actions = ["sts:AssumeRole"]

                                           })
                                       ]
                                   })
                               });
                                               

        return role.Ref;
    }
}
