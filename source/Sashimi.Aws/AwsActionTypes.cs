namespace Sashimi.Aws
{
    public static class AwsActionTypes
    {
        public const string RunScript = "Octopus.AwsRunScript";
        public const string RunCloudFormation = "Octopus.AwsRunCloudFormation";
        public const string DeleteCloudFormation = "Octopus.AwsDeleteCloudFormation";
        public const string UploadS3 = "Octopus.AwsUploadS3";
        public const string ApplyCloudFormationChangeset = "Octopus.AwsApplyCloudFormationChangeSet";
    }
}