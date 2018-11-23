using System.Text.RegularExpressions;
using Amazon.IdentityManagement;
using Amazon.IdentityManagement.Model;
using Amazon.Runtime;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using Calamari.Aws.Integration;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.Aws.Deployment.Conventions
{
    public class LogAwsUserInfoConvention: IInstallConvention
    {
        private readonly IAwsEnvironmentGeneration awsEnvironmentGeneration;
        /// <summary>
        /// Matches ARNs like arn:aws:iam::123456789:role/AWSTestRole and extracts the name as group 1 
        /// </summary>
        private static readonly Regex ArnNameRe = new Regex("^.*?/(.+)$");

        public LogAwsUserInfoConvention(IAwsEnvironmentGeneration awsEnvironmentGeneration)
        {
            this.awsEnvironmentGeneration = awsEnvironmentGeneration;
        }

        public void Install(RunningDeployment deployment)
        {
            Guard.NotNull(deployment, "deployment can not be null");

            if (deployment.Variables.IsSet(SpecialVariables.Action.Aws.AssumeRoleARN) ||
                !deployment.Variables.IsSet(SpecialVariables.Action.Aws.AccountId) ||
                !deployment.Variables.IsSet(deployment.Variables.Get(SpecialVariables.Action.Aws.AccountId) +
                                            ".AccessKey"))
            {
                WriteRoleInfo();
            }
            else
            {
                WriteUseInfo();
            }
        }

        private void WriteUseInfo()
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

        private void WriteRoleInfo()
        {
            try
            {
                new AmazonSecurityTokenServiceClient(awsEnvironmentGeneration.AwsCredentials,
                        new AmazonSecurityTokenServiceConfig
                        {
                            RegionEndpoint = awsEnvironmentGeneration.AwsRegion,
                            ProxyPort = awsEnvironmentGeneration.ProxyPort,
                            ProxyCredentials = awsEnvironmentGeneration.ProxyCredentials,
                            ProxyHost = awsEnvironmentGeneration.ProxyHost
                        })
                    // Client becomes the response of the API call
                    .Map(client => client.GetCallerIdentity(new GetCallerIdentityRequest()))
                    // The response is narrowed to the Aen
                    .Map(response => response.Arn)
                    // Try and match the response to get just the role
                    .Map(arn => ArnNameRe.Match(arn))
                    // Extract the role name, or a default
                    .Map(match => match.Success ? match.Groups[1].Value : "Unknown")
                    // Log the output
                    .Tee(role => Log.Info($"Running the step as the AWS role {role}"));
            }
            catch (AmazonServiceException)
            {
                // Ignore, we just won't add this to the logs
            }
        }
    }
}
