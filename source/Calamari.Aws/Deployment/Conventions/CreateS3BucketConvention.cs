using System;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Calamari.Aws.Integration;
using Calamari.Aws.Util;
using Calamari.CloudAccounts;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Logging;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;

namespace Calamari.Aws.Deployment.Conventions
{
    public class CreateS3BucketConvention: IInstallConvention
    {
        private readonly AwsEnvironmentGeneration awsEnvironmentGeneration;
        private readonly Func<RunningDeployment, string> bucketFactory;

        public CreateS3BucketConvention(AwsEnvironmentGeneration awsEnvironmentGeneration, Func<RunningDeployment, string> bucketFactory)
        {
            this.awsEnvironmentGeneration = awsEnvironmentGeneration;
            this.bucketFactory = bucketFactory;
        }

        public void Install(RunningDeployment deployment)
        {
            InstallAsync(deployment).GetAwaiter().GetResult();
        }

        private Task InstallAsync(RunningDeployment deployment)
        {
            Guard.NotNull(deployment, "Deployment should not be null");
            Guard.NotNull(bucketFactory, "Bucket factory should not be null");

            AmazonS3Client ClientFactory() => ClientHelpers.CreateS3Client(awsEnvironmentGeneration);
            return EnsureBucketExists(ClientFactory, bucketFactory(deployment));
        }

        public async Task EnsureBucketExists(Func<AmazonS3Client> clientFactory, string bucketName)
        {
            Guard.NotNull(clientFactory, "Client factory should not be null");
            Guard.NotNullOrWhiteSpace(bucketName, "Bucket name should not be null or empty");

            using (var client = clientFactory())
            {
                if (await Amazon.S3.Util.AmazonS3Util.DoesS3BucketExistV2Async(client, bucketName))
                {
                    Log.Verbose($"Bucket {bucketName} exists in region {awsEnvironmentGeneration.AwsRegion}. Skipping creation.");
                    return;
                }

                var request = new PutBucketRequest
                {
                    BucketName = bucketName.Trim(),
                    UseClientRegion = true
                };

                Log.Info($"Creating {bucketName}.");
                await client.PutBucketAsync(request);
            }
        }
    }
}
