using System.Linq;
using Amazon.CDK;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.ECS;
using Calamari.Aws.Inputs;

namespace Calamari.Aws.Integration.Ecs;

public class EcsDeployTemplate(DeployEcsCommandInputs commandInputs, App scope, string id, IStackProps props = null)
    : Stack(scope, id, props)
{
    public void GenerateTemplate()
    {
        
        /***
         * // This creates a lightweight reference token instead of generating a massive VPC template
           var cluster = Cluster.FromClusterAttributes(this, "ImportedCluster", new ClusterAttributes
           {
               ClusterName = commandInputs.ClusterName,
               SecurityGroups = new[] { SecurityGroup.FromSecurityGroupId(this, "ClusterSg", "sg-xxxxxxxx") } 
           });
         */
        var cluster = new Cluster(this, commandInputs.ClusterName); // TODO: Handle deploying to an existing cluster

       
        
        var taskDefinition = new FargateTaskDefinition(this,
                                                       commandInputs.TaskName,
                                                       new FargateTaskDefinitionProps
                                                       {
                                                           Cpu = commandInputs.Cpu,
                                                           MemoryLimitMiB = commandInputs.Memory,
                                                           RuntimePlatform = new RuntimePlatform
                                                           {
                                                               OperatingSystemFamily = OperatingSystemFamily.LINUX, // Hardcode to Linux as it's all we support
                                                               CpuArchitecture = commandInputs.CpuArchitecture,
                                                           }
                                                       });

        var fargateService = new FargateService(this,
                                                commandInputs.ServiceName,
                                                new FargateServiceProps
                                                {
                                                    Cluster = cluster,
                                                    TaskDefinition = taskDefinition,
                                                    DesiredCount = commandInputs.DesiredCount, 
                                                    MinHealthyPercent = commandInputs.MinimumHealthyPercentage,
                                                    MaxHealthyPercent = commandInputs.MaximumHealthyPercentage,
                                                    AssignPublicIp =  commandInputs.AutoAssignPublicIp,
                                                    VpcSubnets = new SubnetSelection
                                                    {
                                                        Subnets = commandInputs.SubnetIDs.
                                                                                Select((id, index) => Subnet.FromSubnetId(this, $"Subnet-{index}", id))
                                                                               .ToArray()
                                                    },
                                                    SecurityGroups = commandInputs.NetworkSecurityGroupIds
                                                                                  .Select((id, index) => SecurityGroup.FromSecurityGroupId(this, $"sg-{index}", id))
                                                                                  .ToArray()
                                                });
    }
}

/*
 *                    

   public const string EnableEcsManagedTags =  $"{DeployPrefix}EnableEcsManagedTags";
   public const string TaskRole =  $"{DeployPrefix}TaskRole";
   public const string TaskExecutionRole =  $"{DeployPrefix}TaskExecutionRole";
*/