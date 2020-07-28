using System.Threading;

namespace Calamari.Aws
{
    public static class SpecialVariableNames
    {
        public static class Action
        {
            public const string WaitForCompletion = "Octopus.Action.Aws.WaitForCompletion";
            public const string DisableRollBack = "Octopus.Action.Aws.DisableRollback";
            public const string EnabledFeatures = "Octopus.Action.EnabledFeatures";
        }

        public static class Aws
        {
            public const string IamCapabilities = "Octopus.Action.Aws.IamCapabilities";
            public const string AssumeRoleARN = "Octopus.Action.Aws.AssumedRoleArn";
            public const string AccountId = "Octopus.Action.AwsAccount.Variable";

            public static class S3
            {
                public const string BucketName = "Octopus.Action.Aws.S3.BucketName";
                public const string FileSelections = "Octopus.Action.Aws.S3.FileSelections";
                public const string PackageOptions = "Octopus.Action.Aws.S3.PackageOptions";
                public const string TargetMode = "Octopus.Action.Aws.S3.TargetMode";
            }

            public static class CloudFormation
            {
                public const string Action = "Octopus.Action.Aws.CloudFormationAction";
                public const string StackName = "Octopus.Action.Aws.CloudFormationStackName";
                public const string Template = "Octopus.Action.Aws.CloudFormationTemplate";
                public const string TemplateParameters = "Octopus.Action.Aws.CloudFormationTemplateParameters";
                public const string TemplateParametersRaw = "Octopus.Action.Aws.CloudFormationTemplateParametersRaw";
                public const string Properties = "Octopus.Action.Aws.CloudFormationProperties";
                public const string RoleArn = "Octopus.Action.Aws.CloudFormation.RoleArn";
                public const string TemplateSource = "Octopus.Action.Aws.TemplateSource";

                public static class ChangeSets
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
}
