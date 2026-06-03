namespace Calamari.Aws.Deployment
{
    public static class AwsSpecialVariables
    {
        public const string IamCapabilities = "Octopus.Action.Aws.IamCapabilities";
        public const string ResourceTags = "Octopus.Action.Aws.Tags";

        public static class Authentication
        {
            public const string UseInstanceRole = "Octopus.Action.AwsAccount.UseInstanceRole";
            public const string AwsAccountVariable = "Octopus.Action.AwsAccount.Variable";
        }

        public static class S3
        {
            public const string BucketName = "Octopus.Action.Aws.S3.BucketName";
            public const string ObjectWriterOwnership = "Octopus.Action.Aws.S3.ObjectWriterOwnership";
            public const string PublicAccess = "Octopus.Action.Aws.S3.PublicAccess";
            public const string FileSelections = "Octopus.Action.Aws.S3.FileSelections";
            public const string PackageOptions = "Octopus.Action.Aws.S3.PackageOptions";
        }

        public static class Ecs
        {
            public static class Deploy
            {

                // Not reusing CloudFormation variable here to make it easier to remove all traces of this when we migrate to native ECS API
                public const string StackName = "Octopus.Action.Aws.Ecs.Deploy.CFStackName";

                public const string DesiredCount =  "Octopus.Action.Aws.Ecs.Deploy.DesiredCount";
                public const string MinimumHealthPercent =  "Octopus.Action.Aws.Ecs.Deploy.MinimumHealthPercent";
                public const string MaximumHealthPercent =  "Octopus.Action.Aws.Ecs.Deploy.MaximumHealthPercent";
                public const string Cpu =  "Octopus.Action.Aws.Ecs.Deploy.Cpu";
                public const string Memory =  "Octopus.Action.Aws.Ecs.Deploy.Memory";
                public const string RuntimeArchitecturePlatform =  "Octopus.Action.Aws.Ecs.Deploy.RuntimeArchitecturePlatform";
                public const string AutoAssignPublicIp =  "Octopus.Action.Aws.Ecs.Deploy.AutoAssignPublicIp";
                public const string EnableEcsManagedTags =  "Octopus.Action.Aws.Ecs.Deploy.EnableEcsManagedTags";
                public const string ServiceTaskName =  "Octopus.Action.Aws.Ecs.Deploy.ServiceTaskName";
                public const string TaskRole =  "Octopus.Action.Aws.Ecs.Deploy.TaskRole";
                public const string TaskExecutionRole =  "Octopus.Action.Aws.Ecs.Deploy.TaskExecutionRole";
                public const string SecurityGroupIds =  "Octopus.Action.Aws.Ecs.Deploy.SecurityGroupIds";
                public const string SubnetIds =   "Octopus.Action.Aws.Ecs.Deploy.SubnetIds";
                public const string LoadBalancerMappings =   "Octopus.Action.Aws.Ecs.Deploy.LoadBalancerMappings";
                public const string Volumes = "Octopus.Action.Aws.Ecs.Deploy.Volumes";
                public const string Containers = "Octopus.Action.Aws.Ecs.Deploy.Containers";
            }
            
            // Not reusing CloudFormation variable here to make it easier to remove all traces of this when we migrate to native ECS API
            public const string Tags = "Octopus.Action.Aws.Ecs.Tags";
            public const string ClusterName = "Octopus.Action.Aws.Ecs.ClusterName";
            public const string WaitOption = "Octopus.Action.Aws.Ecs.WaitOption";
            

            public static class Update
            {
                public const string ServiceName = "Octopus.Action.Aws.Ecs.Update.ServiceName";
                public const string TargetTaskDefinitionName = "Octopus.Action.Aws.Ecs.Update.TargetTaskDefinitionName";
                public const string TemplateTaskDefinitionName = "Octopus.Action.Aws.Ecs.Update.TemplateTaskDefinitionName";
                public const string ContainerUpdates = "Octopus.Action.Aws.Ecs.Update.ContainerUpdates";
            }
        }

        public static class CloudFormation
        {
            public const string Action = "Octopus.Action.Aws.CloudFormationAction";
            public const string StackName = "Octopus.Action.Aws.CloudFormationStackName";
            public const string Template = "Octopus.Action.Aws.CloudFormationTemplate";
            public const string TemplateParameters = "Octopus.Action.Aws.CloudFormationTemplateParameters";
            public const string TemplateParametersRaw = "Octopus.Action.Aws.CloudFormationTemplateParametersRaw";
            public const string RoleArn = "Octopus.Action.Aws.CloudFormation.RoleArn";
            // TODO: Tags aren't CFN specific so migrate to use ResourceTags = "Octopus.Action.Aws.Tags" above
            public const string Tags = "Octopus.Action.Aws.CloudFormation.Tags";

            public static class Changesets
            {
                public const string Feature = "Octopus.Features.CloudFormation.ChangeSet.Feature";
                //The Name is generally used when the user doesn't want octopus to generate the change set name
                public const string Name = "Octopus.Action.Aws.CloudFormation.ChangeSet.Name";
                public const string Defer = "Octopus.Action.Aws.CloudFormation.ChangeSet.Defer";
                public const string Generate = "Octopus.Action.Aws.CloudFormation.ChangeSet.GenerateName";
                //The ARN is either specified or dynamically provided when the change set is created
                public const string Arn = "Octopus.Action.Aws.CloudFormation.ChangeSet.Arn";
            }
        }
    }
}
