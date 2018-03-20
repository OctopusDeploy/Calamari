using Amazon.IdentityManagement;
using Amazon.IdentityManagement.Model;
using Amazon.Runtime;
using Calamari.Aws.Integration;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.Aws.Deployment.Conventions
{
    public class LogAwsUserInfoConvention: IInstallConvention
    {
        private readonly IAwsEnvironmentGeneration awsEnvironmentGeneration;

        public LogAwsUserInfoConvention(IAwsEnvironmentGeneration awsEnvironmentGeneration)
        {
            this.awsEnvironmentGeneration = awsEnvironmentGeneration;
        }

        public void Install(RunningDeployment deployment)
        {
            try
            {
                new AmazonIdentityManagementServiceClient(awsEnvironmentGeneration.AwsCredentials,
                        new AmazonIdentityManagementServiceConfig
                        {
                            RegionEndpoint = awsEnvironmentGeneration.AwsRegion,
                            ProxyPort = awsEnvironmentGeneration.ProxyPort,
                            ProxyCredentials = awsEnvironmentGeneration.ProxyCredentials,
                            ProxyHost = awsEnvironmentGeneration.ProxyHost
                        })
                    // The client becomes the API response
                    .Map(client => client.GetUser(new GetUserRequest()))
                    // Log the details of the response
                    .Tee(response => Log.Info($"Running the step as the AWS user {response.User.UserName}"));
            }
            catch (AmazonServiceException)
            {
                // Ignore, we just won't add this to the logs
            }
        }
    }
}
