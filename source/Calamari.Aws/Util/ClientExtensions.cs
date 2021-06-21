using Amazon.CloudFormation;
using Amazon.IdentityManagement;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.SecurityToken;
using Calamari.Aws.Integration;
using Calamari.CloudAccounts;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.Aws.Util
{
    public static class ClientExtensions
    {
        public static TConfig AsClientConfig<TConfig>(this AwsEnvironmentGeneration environment)
            where TConfig : ClientConfig, new()
        {
            return new TConfig().Tee(x =>
            {
                x.RegionEndpoint = environment.AwsRegion;
                x.AllowAutoRedirect = true;
            });
        }}

    public static class ClientHelpers
    {
        public static AmazonIdentityManagementServiceClient CreateIdentityManagementServiceClient(
            AwsEnvironmentGeneration environment)
        {
            return new AmazonIdentityManagementServiceClient(environment.AwsCredentials, environment.AsClientConfig<AmazonIdentityManagementServiceConfig>());
        }
        public static AmazonSecurityTokenServiceClient CreateSecurityTokenServiceClient(
            AwsEnvironmentGeneration environment)
        {
            return new AmazonSecurityTokenServiceClient(environment.AwsCredentials, environment.AsClientConfig<AmazonSecurityTokenServiceConfig>());
        }
        public static AmazonS3Client CreateS3Client(AwsEnvironmentGeneration environment)
        {
            return new AmazonS3Client(environment.AwsCredentials, environment.AsClientConfig<AmazonS3Config>());
        }

        public static IAmazonCloudFormation CreateCloudFormationClient(AwsEnvironmentGeneration environment)
        {
            return new AmazonCloudFormationClient(environment.AwsCredentials,
                environment.AsClientConfig<AmazonCloudFormationConfig>());
        }
    }
}
