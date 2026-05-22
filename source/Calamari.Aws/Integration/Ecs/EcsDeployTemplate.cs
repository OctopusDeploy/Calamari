using System;
using System.Collections.Generic;
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

        var taskDefinition = new CfnTaskDefinition(this,
                                                   commandInputs.TaskName,
                                                   new CfnTaskDefinitionProps
                                                   {
                                                       Family = taskFamilyParam.ValueAsString,
                                                       Cpu = cpuParam.ValueAsString,
                                                       Memory = memoryParam.ValueAsString,
                                                       NetworkMode = AwsVpcNetworkMode,
                                                       RequiresCompatibilities = [FargateLaunchType],
                                                       ExecutionRoleArn = executionRoleRef,
                                                       TaskRoleArn = string.IsNullOrEmpty(commandInputs.TaskRole) ? null : commandInputs.TaskRole,
                                                       RuntimePlatform = new CfnTaskDefinition.RuntimePlatformProperty
                                                       {
                                                           OperatingSystemFamily = LinuxOperatingSystemFamily,
                                                           CpuArchitecture = commandInputs.CpuArchitecture
                                                       },
                                                       ContainerDefinitions = new[]
                                                       {
                                                           // TODO: Read from variables
                                                           new CfnTaskDefinition.ContainerDefinitionProperty
                                                           {
                                                               Name = "sample-container",
                                                               Image = "index.docker.io/nginx:1.31",
                                                               Essential = true,
                                                               ResourceRequirements = Array.Empty<CfnTaskDefinition.ResourceRequirementProperty>(),
                                                               EnvironmentFiles = Array.Empty<CfnTaskDefinition.EnvironmentFileProperty>(),
                                                               DisableNetworking = false,
                                                               DnsServers = Array.Empty<string>(),
                                                               DnsSearchDomains = Array.Empty<string>(),
                                                               ExtraHosts = Array.Empty<CfnTaskDefinition.HostEntryProperty>(),
                                                               PortMappings = new[]
                                                               {
                                                                   new CfnTaskDefinition.PortMappingProperty
                                                                   {
                                                                       ContainerPort = 80,
                                                                       HostPort = 80,
                                                                       Protocol = "tcp"
                                                                   }
                                                               }
                                                           }
                                                       },
                                                       Volumes = Array.Empty<CfnTaskDefinition.VolumeProperty>(), // TODO: Read from variables
                                                       Tags = Array.Empty<CfnTag>()
                                                   });

        var service = new CfnService(this,
                                     commandInputs.ServiceName,
                                     new CfnServiceProps
                                     {
                                         Cluster = clusterNameParam.ValueAsString,
                                         LaunchType = FargateLaunchType,
                                         TaskDefinition = taskDefinition.Ref,
                                         DesiredCount = commandInputs.DesiredCount,
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
                                         EnableEcsManagedTags = commandInputs.EnableEcsManagedTags,
                                         Tags = Array.Empty<CfnTag>()
                                     });

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
                                   AssumeRolePolicyDocument = new Dictionary<string, object>
                                   {
                                       ["Version"] = "2012-10-17",
                                       ["Statement"] = new[]
                                       {
                                           new Dictionary<string, object>
                                           {
                                               ["Effect"] = "Allow",
                                               ["Principal"] = new Dictionary<string, object>
                                               {
                                                   ["Service"] = new[] { "ecs-tasks.amazonaws.com" }
                                               },
                                               ["Action"] = new[] { "sts:AssumeRole" }
                                           }
                                       }
                                   },
                                   ManagedPolicyArns = new[] { policyArnParam.ValueAsString }
                               });

        return role.Ref;
    }
}
