using Amazon.CDK;
using Amazon.CDK.AWS.ECS;
using Calamari.Aws.Inputs;

namespace Calamari.Aws.Integration.Ecs;

public class EcsDeployTemplate(DeployEcsCommandInputs commandInputs, App scope, string id, IStackProps props = null)
    : Stack(scope, id, props)
{
    public void GenerateTemplate()
    {
        var cluster = new Cluster(this, commandInputs.ClusterName);

        var taskDefinition = new FargateTaskDefinition(this,
                                                       commandInputs.TaskName,
                                                       new FargateTaskDefinitionProps
                                                       {
                                                           Cpu = 0, // TODO: From Variables
                                                           MemoryLimitMiB = 0, // TODO: From Variables
                                                           RuntimePlatform = new RuntimePlatform
                                                           {
                                                               OperatingSystemFamily = OperatingSystemFamily.LINUX, // Hardcode to Linux as it's all we support
                                                               CpuArchitecture = CpuArchitecture.X86_64 // TODO: from Variables
                                                           }
                                                       });

        var fargateService = new FargateService(this,
                                                commandInputs.ServiceName,
                                                new FargateServiceProps
                                                {
                                                    Cluster = cluster,
                                                    TaskDefinition = taskDefinition,
                                                    DesiredCount = 1, // TODO: Variables
                                                    MinHealthyPercent = 100, //TODO: Variables
                                                    MaxHealthyPercent = 200, // TODO: Variables
                                                });
    }
}

/*
 *                         public const string StackName = $"{DeployPrefix}CFStackName";

   public const string DesiredCount =  $"{DeployPrefix}DesiredCount";
   public const string MinimumHealthPercent =  $"{DeployPrefix}MinimumHealthPercent";
   public const string MaximumHealthPercent =  $"{DeployPrefix}MaximumHealthPercent";
   public const string Cpu =  $"{DeployPrefix}Cpu";
   public const string Memory =  $"{DeployPrefix}Memory";
   public const string RuntimeArchitecturePlatform =  $"{DeployPrefix}RuntimeArchitecturePlatform";
   public const string AutoAssignPublicIp =  $"{DeployPrefix}AutoAssignPublicIp";
   public const string EnableEcsManagedTags =  $"{DeployPrefix}EnableEcsManagedTags";
   public const string TaskDefinitionName =  $"{DeployPrefix}TaskDefinitionName";
   public const string TaskRole =  $"{DeployPrefix}TaskRole";
   public const string TaskExecutionRole =  $"{DeployPrefix}TaskExecutionRole";
*/