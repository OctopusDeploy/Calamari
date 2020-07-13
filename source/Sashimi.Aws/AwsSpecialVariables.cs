namespace Sashimi.Aws
{
    static class AwsSpecialVariables
    {
        public static class Action
        {
            public static class Aws
            {
                public const string TemplateSource = "Octopus.Action.Aws.TemplateSource";
                public static class TemplateSourceOptions
                {
                    public const string Package = "Package";
                    public const string Inline = "Inline";
                }

                public const string WaitForCompletion = "Octopus.Action.Aws.WaitForCompletion";
                public const string DisableRollback = "Octopus.Action.Aws.DisableRollback";

                public static readonly string UseBundledAwsPowerShellModules = "OctopusUseBundledAwsPowerShellModules";
                public static readonly string UseBundledAwsCLI = "OctopusUseBundledAwsCLI";

                public static class CloudFormation
                {
                    //TODO: The naming of these variables slipped through without proper namespacing, refactor at some point and perform a data migration
                    public const string StackName = "Octopus.Action.Aws.CloudFormationStackName";
                    public const string Template = "Octopus.Action.Aws.CloudFormationTemplate";
                    public const string TemplateParameters = "Octopus.Action.Aws.CloudFormationTemplateParameters";
                    public const string TemplateParametersRaw = "Octopus.Action.Aws.CloudFormationTemplateParametersRaw";
                    public const string RoleArn = "Octopus.Action.Aws.CloudFormation.RoleArn";

                    public static class Changesets
                    {
                        public const string Feature = "Octopus.Features.CloudFormation.ChangeSet.Feature";
                        public const string Name = "Octopus.Action.Aws.CloudFormation.ChangeSet.Name";
                        public const string Defer = "Octopus.Action.Aws.CloudFormation.ChangeSet.Defer";
                        public const string Generate = "Octopus.Action.Aws.CloudFormation.ChangeSet.GenerateName";
                        public const string Arn = "Octopus.Action.Aws.CloudFormation.ChangeSet.Arn";
                    }
                }

                public static class S3
                {
                    public const string BucketName = "Octopus.Action.Aws.S3.BucketName";
                    public const string FileSelections = "Octopus.Action.Aws.S3.FileSelections";
                    public const string PackageOptions = "Octopus.Action.Aws.S3.PackageOptions";
                    public const string TargetMode = "Octopus.Action.Aws.S3.TargetMode";
                }

            }
        }
    }
}