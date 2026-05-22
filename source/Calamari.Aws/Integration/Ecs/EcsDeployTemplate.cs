using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Amazon.CDK;
using Amazon.CDK.AWS.ECS;
using Amazon.CDK.AWS.IAM;
using Calamari.Aws.Inputs;


namespace Calamari.Aws.Integration.Ecs;

public class EcsDeployTemplate : Stack
{
    const string FargateLaunchType = "FARGATE";
    const string AwsVpcNetworkMode = "awsvpc";
    const string LinuxOperatingSystemFamily = "LINUX";
    const string DefaultTaskExecutionPolicyArn = "arn:aws:iam::aws:policy/service-role/AmazonECSTaskExecutionRolePolicy";

    readonly DeployEcsCommandInputs commandInputs;

    public EcsDeployTemplate(DeployEcsCommandInputs commandInputs, App scope, string id, IStackProps props = null) : base(scope, id, props)
    {
        this.commandInputs = commandInputs;

        var executionRoleArn = ProcessTaskExecutionRole(commandInputs);

        var taskDefinition = new CfnTaskDefinition(this,
                                                   commandInputs.TaskName,
                                                   new CfnTaskDefinitionProps
                                                   {
                                                       Family = commandInputs.TaskName,
                                                       Cpu = commandInputs.Cpu,
                                                       Memory = commandInputs.Memory,
                                                       NetworkMode = AwsVpcNetworkMode,
                                                       RequiresCompatibilities = [FargateLaunchType],
                                                       ExecutionRoleArn = executionRoleArn,
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
                                                               Name = "placeholder",
                                                               Image = "index.docker.io/nginx:latest",
                                                               Essential = true
                                                           }
                                                       },
                                                       Volumes = Array.Empty<CfnTaskDefinition.VolumeProperty>() // TODO: Read from variables
                                                   });

        _ = new CfnService(this,
                           commandInputs.ServiceName,
                           new CfnServiceProps
                           {
                               ServiceName = commandInputs.ServiceName,
                               Cluster = commandInputs.ClusterName,
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
                               LoadBalancers = Array.Empty<CfnService.LoadBalancerProperty>(), // TODO: Read from variables
                               VolumeConfigurations = Array.Empty<CfnService.ServiceVolumeConfigurationProperty>() // TODO: Read from variables
                           });
    }

    string ProcessTaskExecutionRole(DeployEcsCommandInputs inputs)
    {
        if (!string.IsNullOrEmpty(inputs.TaskExecutionRole))
        {
            return inputs.TaskExecutionRole;
        }

        var role = new CfnRole(this,
                               "DefaultTaskExecutionRole",
                               new CfnRoleProps
                               {
                                   RoleName = inputs.FallbackTaskExecutionRoleName,
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
                                                   ["Service"] = "ecs-tasks.amazonaws.com"
                                               },
                                               ["Action"] = "sts:AssumeRole"
                                           }
                                       }
                                   },
                                   ManagedPolicyArns = new[] { DefaultTaskExecutionPolicyArn }
                               });

        return role.AttrArn;
    }
}
