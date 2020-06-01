namespace Calamari.Aws
{
    public static class KnownAwsCalamariCommands
    {
        public static class Commands
        {
            //TODO: actually implement RunScript!
            public const string RunScript = "run-script";
            public const string ApplyAwsCloudFormationChangeSet = "apply-aws-cloudformation-changeset";
            public const string DeleteAwsCloudFormation = "delete-aws-cloudformation";
            public const string DeployAwsCloudFormation = "deploy-aws-cloudformation";
            public const string UploadAwsS3 = "upload-aws-s3";
        }
    }
}