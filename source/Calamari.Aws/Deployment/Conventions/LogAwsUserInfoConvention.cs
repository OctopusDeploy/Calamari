using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Amazon.IdentityManagement;
using Amazon.IdentityManagement.Model;
using Amazon.Runtime;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using Calamari.Aws.Integration;
using Calamari.Aws.Util;
using Calamari.CloudAccounts;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Logging;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.Aws.Deployment.Conventions
{
    public class LogAwsUserInfoConvention: IInstallConvention
    {
        private readonly AwsEnvironmentGeneration awsEnvironmentGeneration;
        /// <summary>
        /// Matches ARNs like arn:aws:iam::123456789:role/AWSTestRole and extracts the name as group 1 
        /// </summary>
        private static readonly Regex ArnNameRe = new Regex("^.*?/(.+)$");

        public LogAwsUserInfoConvention(AwsEnvironmentGeneration awsEnvironmentGeneration)
        {
            this.awsEnvironmentGeneration = awsEnvironmentGeneration;
        }

        public void Install(RunningDeployment deployment)
        {
            InstallAsync(deployment).GetAwaiter().GetResult();
        }

        private async Task InstallAsync(RunningDeployment deployment)
        {
            Guard.NotNull(deployment, "deployment can not be null");
            Func<IAmazonSecurityTokenService> stsClientFactory = () => ClientHelpers.CreateSecurityTokenServiceClient(awsEnvironmentGeneration);
            Func<IAmazonIdentityManagementService> identityManagementClientFactory = () =>
                ClientHelpers.CreateIdentityManagementServiceClient(awsEnvironmentGeneration);
            

            if (deployment.Variables.IsSet(SpecialVariables.Action.Aws.AssumeRoleARN) ||
                !deployment.Variables.IsSet(SpecialVariables.Action.Aws.AccountId) ||
                !deployment.Variables.IsSet(deployment.Variables.Get(SpecialVariables.Action.Aws.AccountId) +
                                            ".AccessKey"))
            {
                await WriteRoleInfo(stsClientFactory);
            }
            else
            {
                await WriteUseInfo(identityManagementClientFactory);
            }
        }

        private async Task WriteUseInfo(Func<IAmazonIdentityManagementService> clientFactory)
        {
            try
            {
                var result = await clientFactory().GetUserAsync(new GetUserRequest());
                Log.Info($"Running the step as the AWS user {result.User.UserName}");
            }
            catch (AmazonServiceException)
            {
                // Ignore, we just won't add this to the logs
            }
        }

        private async Task WriteRoleInfo(Func<IAmazonSecurityTokenService> clientFactory)
        {
            try
            {
                (await clientFactory().GetCallerIdentityAsync(new GetCallerIdentityRequest()))
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
