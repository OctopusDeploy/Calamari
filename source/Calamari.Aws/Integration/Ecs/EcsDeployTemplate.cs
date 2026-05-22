using System.Globalization;
using System.Linq;
using Amazon.CDK;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.ECS;
using Amazon.CDK.AWS.IAM;
using Calamari.Aws.Inputs;


namespace Calamari.Aws.Integration.Ecs;

public class EcsDeployTemplate : Stack
{
    readonly DeployEcsCommandInputs commandInputs;

    public  EcsDeployTemplate(DeployEcsCommandInputs commandInputs, App scope, string id, IStackProps props = null): base(scope, id, props)
    {
        this.commandInputs = commandInputs;
        
        /***
         * // This creates a lightweight reference token instead of generating a massive VPC template
           var cluster = Cluster.FromClusterAttributes(this, "ImportedCluster", new ClusterAttributes
           {
               ClusterName = commandInputs.ClusterName,
               SecurityGroups = new[] { SecurityGroup.FromSecurityGroupId(this, "ClusterSg", "sg-xxxxxxxx") } 
           });
         */
        // var cluster = new Cluster(this, commandInputs.ClusterName); // TODO: Handle deploying to an existing cluster
        

        var cluster = Cluster.FromClusterAttributes(this,
                                                    "ImportedCluster",
                                                    new ClusterAttributes
                                                    {
                                                        ClusterName = commandInputs.ClusterName,
                                                        
                                                        // We must have this fake VPC otherwise CDK goes 💥
                                                        Vpc = Vpc.FromVpcAttributes(this, "ClusterVpcContext", new VpcAttributes
                                                        {
                                                            VpcId = "vpc-dummy",
                                                            AvailabilityZones = ["ap-southeast-2a", "ap-southeast-2b"]
                                                        })
                                                    });
       
        
        var taskDefinition = new TaskDefinition(this,
                                                commandInputs.TaskName,
                                                new TaskDefinitionProps
                                                {
                                                    Cpu = commandInputs.Cpu.ToString(CultureInfo.InvariantCulture),
                                                    MemoryMiB = commandInputs.Memory.ToString(CultureInfo.InvariantCulture),
                                                    RuntimePlatform = new RuntimePlatform
                                                    {
                                                        OperatingSystemFamily = OperatingSystemFamily.LINUX, // Hardcode to Linux as it's all we support
                                                        CpuArchitecture = commandInputs.CpuArchitecture,
                                                    },
                                                    ExecutionRole = ProcessTaskExecutionRole(commandInputs),
                                                    TaskRole =  string.IsNullOrEmpty(commandInputs.TaskExecutionRole) ? null : Role.FromRoleArn(this, "SuppliedTaskRole", commandInputs.TaskExecutionRole),
                                                    Volumes = [], //TODO: Read from Variables
                                                           
                                                });

        // TODO: Add Containers

        taskDefinition.AddContainer("id", new ContainerDefinitionProps
        {
            Essential = true,
            Image = ContainerImage.FromRegistry("index.docker.io/nginx:latest", new RepositoryImageProps())
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
                                                                                  .ToArray(),
                                                    EnableECSManagedTags =  commandInputs.EnableEcsManagedTags,
                                                    VolumeConfigurations = [], //TODO: Read from variables
                                                });
        
        // Amazon.CDK.Tags.Of(fargateService).Add("TagName", "TagValue");
        // fargateService.AddVolume();
        // fargateService.LoadBalancers = [
        //
        //     new CfnService.LoadBalancerProperty()
        //     {
        //         
        //     }
        // ];
    }

    IRole ProcessTaskExecutionRole(DeployEcsCommandInputs inputs)
    {
        if (string.IsNullOrEmpty(inputs.TaskExecutionRole))
        {
            var role = new Role(this, "DefaultTaskExecutionRole", new RoleProps
            {
                RoleName = inputs.FallbackTaskExecutionRoleName,
                AssumedBy = new ServicePrincipal("ecs-tasks.amazonaws.com"),
                Path = "/",
                ManagedPolicies = [] //TODO: Populate
            });

            _ = new CfnParameter(this,
                                 "AmazonECSTaskExecutionRolePolicyArn",
                                 new CfnParameterProps
                                 {
                                     Type = "String",
                                     Default = "arn:aws:iam::aws:policy/service-role/AmazonECSTaskExecutionRolePolicy",
                                 });

            return role;


        }

        return Role.FromRoleArn(this, "SuppliedTaskExecutionRole", commandInputs.TaskExecutionRole);
    }

    
}

/*
 *                    


   public const string TaskRole =  $"{DeployPrefix}TaskRole";
   public const string TaskExecutionRole =  $"{DeployPrefix}TaskExecutionRole";
*/