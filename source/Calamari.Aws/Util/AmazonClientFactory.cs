using System;
using System.Threading.Tasks;
using Amazon.CloudFormation;
using Amazon.IdentityManagement;
using Amazon.S3;
using Amazon.SecurityToken;
using Calamari.CloudAccounts;

namespace Calamari.Aws.Util
{
    public class AmazonClientFactory : IAmazonClientFactory
    {
        readonly Lazy<Task<AwsEnvironmentGeneration>> awsEnvironment;

        public AmazonClientFactory(Lazy<Task<AwsEnvironmentGeneration>> awsEnvironment)
        {
            this.awsEnvironment = awsEnvironment;
        }

        public async Task<IAmazonS3> GetS3Client()
        {
            var awsEnv = await awsEnvironment.Value;

            return new AmazonS3Client(awsEnv.AwsCredentials, awsEnv.AwsRegion);
        }

        public async Task<IAmazonIdentityManagementService> GetIdentityManagementClient()
        {
            var awsEnv = await awsEnvironment.Value;

            return new AmazonIdentityManagementServiceClient(awsEnv.AwsCredentials, awsEnv.AwsRegion);
        }

        public async Task<IAmazonSecurityTokenService> GetSecurityTokenClient()
        {
            var awsEnv = await awsEnvironment.Value;

            return new AmazonSecurityTokenServiceClient(awsEnv.AwsCredentials, awsEnv.AwsRegion);
        }

        public async Task<IAmazonCloudFormation> GetCloudFormationClient()
        {
            var awsEnv = await awsEnvironment.Value;

            return new AmazonCloudFormationClient(awsEnv.AwsCredentials, awsEnv.AwsRegion);

        }
    }
}