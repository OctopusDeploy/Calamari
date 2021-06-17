using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Newtonsoft.Json;

namespace Calamari.CloudAccounts
{
    /// <summary>
    /// This service is used to generate the appropriate environment variables required to authentication
    /// custom scripts and the C# AWS SDK code exposed as Calamari commands that interact with AWS.
    /// </summary>
    public class AwsEnvironmentGeneration
    {
        const string RoleUri = "http://169.254.169.254/latest/meta-data/iam/security-credentials/";

        readonly ILog log;
        readonly Func<Task<bool>> verifyLogin;
        readonly string region;
        readonly string accessKey;
        readonly string secretKey;
        readonly string assumeRole;
        readonly string assumeRoleArn;
        readonly string assumeRoleExternalId;
        readonly string assumeRoleSession;
        readonly string assumeRoleDurationSeconds;

        public static async Task<AwsEnvironmentGeneration> Create(ILog log, IVariables variables, Func<Task<bool>> verifyLogin = null)
        {
            var environmentGeneration = new AwsEnvironmentGeneration(log, variables, verifyLogin);

            await environmentGeneration.Initialise();

            return environmentGeneration;
        }

        async Task Initialise()
        {
            PopulateCommonSettings();
            await PopulateSuppliedKeys();
            await PopulateKeysFromInstanceRole();
            await AssumeRole();
        }

        public Dictionary<string, string> EnvironmentVars { get; } = new Dictionary<string, string>();

        internal AwsEnvironmentGeneration(ILog log, IVariables variables, Func<Task<bool>> verifyLogin = null)
        {
            this.log = log;
            this.verifyLogin = verifyLogin ?? VerifyLogin;
            var account = variables.Get("Octopus.Action.AwsAccount.Variable")?.Trim();
            region = variables.Get("Octopus.Action.Aws.Region")?.Trim();
            // When building the context for an AWS step, there will be a variable expanded with the keys
            accessKey = variables.Get(account + ".AccessKey")?.Trim() ??
                        // When building a context with an account associated with a target, the keys are under Octopus.Action.Amazon.
                        // The lack of any keys means we rely on an EC2 instance role.
                        variables.Get("Octopus.Action.Amazon.AccessKey")?.Trim();
            secretKey = variables.Get(account + ".SecretKey")?.Trim() ??
                        variables.Get("Octopus.Action.Amazon.SecretKey")?.Trim();
            assumeRole = variables.Get("Octopus.Action.Aws.AssumeRole")?.Trim();
            assumeRoleArn = variables.Get("Octopus.Action.Aws.AssumedRoleArn")?.Trim();
            assumeRoleExternalId = variables.Get("Octopus.Action.Aws.AssumeRoleExternalId")?.Trim();
            assumeRoleSession = variables.Get("Octopus.Action.Aws.AssumedRoleSession")?.Trim();
            assumeRoleDurationSeconds = variables.Get("Octopus.Action.Aws.AssumeRoleSessionDurationSeconds")?.Trim();
        }

        /// <summary>
        /// Depending on how we logged in, we might be using a session or basic credentials
        /// </summary>
        /// <returns>AWS Credentials that can be used by the AWS API</returns>
        public AWSCredentials AwsCredentials
        {
            get
            {
                if (EnvironmentVars.ContainsKey("AWS_SESSION_TOKEN"))
                {
                    return new SessionAWSCredentials(
                        EnvironmentVars["AWS_ACCESS_KEY_ID"],
                        EnvironmentVars["AWS_SECRET_ACCESS_KEY"],
                        EnvironmentVars["AWS_SESSION_TOKEN"]);
                }

                return new BasicAWSCredentials(
                    EnvironmentVars["AWS_ACCESS_KEY_ID"],
                    EnvironmentVars["AWS_SECRET_ACCESS_KEY"]);
            }
        }

        public RegionEndpoint AwsRegion => RegionEndpoint.GetBySystemName(EnvironmentVars["AWS_REGION"]);

        /// <summary>
        /// Verify that we can login with the supplied credentials
        /// </summary>
        /// <returns>true if login succeeds, false otherwise</returns>
        async Task<bool> VerifyLogin()
        {
            try
            {
                await new AmazonSecurityTokenServiceClient(AwsCredentials).GetCallerIdentityAsync(new GetCallerIdentityRequest());
                return true;

            }
            catch (AmazonServiceException ex)
            {
                log.Error("Error occured while verifying login");
                log.Error(ex.Message);
                log.Error(ex.StackTrace);
                // Any exception is considered to be a failed login
                return false;
            }
        }

        /// <summary>
        /// We always set these variables, regardless of the kind of login
        /// </summary>
        void PopulateCommonSettings()
        {
            EnvironmentVars["AWS_DEFAULT_REGION"] = region;
            EnvironmentVars["AWS_REGION"] = region;
        }

        /// <summary>
        /// If the keys were explicitly supplied, use them directly
        /// </summary>
        /// <exception cref="Exception">The supplied keys were not valid</exception>
        async Task PopulateSuppliedKeys()
        {
            if (!String.IsNullOrEmpty(accessKey))
            {
                EnvironmentVars["AWS_ACCESS_KEY_ID"] = accessKey;
                EnvironmentVars["AWS_SECRET_ACCESS_KEY"] = secretKey;
                if (!await verifyLogin())
                {
                    throw new Exception("AWS-LOGIN-ERROR-0005: Failed to verify the credentials. " +
                                             "Please check the keys assigned to the Amazon Web Services Account associated with this step. " +
                                             $"For more information visit {log.FormatLink("https://g.octopushq.com/AwsCloudFormationDeploy#aws-login-error-0005")}");
                }
            }
        }

        /// <summary>
        /// If no keys were supplied, we must be using the instance role
        /// </summary>
        /// <exception cref="Exception">The instance role information could not be extracted</exception>
        async Task PopulateKeysFromInstanceRole()
        {
            if (String.IsNullOrEmpty(accessKey))
            {
                try
                {
                    string payload;
                    using (var client = new HttpClient())
                    {
                        var instanceRole = await client.GetStringAsync(RoleUri);

                        payload = await client.GetStringAsync($"{RoleUri}{instanceRole}");
                    }

                    dynamic instanceRoleKeys = JsonConvert.DeserializeObject(payload);

                    EnvironmentVars["AWS_ACCESS_KEY_ID"] = instanceRoleKeys.AccessKeyId;
                    EnvironmentVars["AWS_SECRET_ACCESS_KEY"] = instanceRoleKeys.SecretAccessKey;
                    EnvironmentVars["AWS_SESSION_TOKEN"] = instanceRoleKeys.Token;
                }
                catch (Exception ex)
                {
                    // This was either a generic error accessing the metadata URI, or accessing the
                    // dynamic properties resulted in an error (which means the response was not
                    // in the expected format).
                    throw new Exception(
                        $"AWS-LOGIN-ERROR-0003: Failed to access the role information under {RoleUri}, " +
                        "or failed to parse the response. This may be because the instance does not have " +
                        "a role assigned to it. " +
                        $"For more information visit {log.FormatLink("https://g.octopushq.com/AwsCloudFormationDeploy#aws-login-error-0003")}", ex);
                }
            }
        }

        /// <summary>
        /// If we assume a secondary role, do it here
        /// </summary>
        async Task AssumeRole()
        {
            if ("True".Equals(assumeRole, StringComparison.OrdinalIgnoreCase))
            {
                var client = new AmazonSecurityTokenServiceClient(AwsCredentials);
                var credentials = (await client.AssumeRoleAsync(GetAssumeRoleRequest())).Credentials;

                EnvironmentVars["AWS_ACCESS_KEY_ID"] = credentials.AccessKeyId;
                EnvironmentVars["AWS_SECRET_ACCESS_KEY"] = credentials.SecretAccessKey;
                EnvironmentVars["AWS_SESSION_TOKEN"] = credentials.SessionToken;
            }
        }

        public AssumeRoleRequest GetAssumeRoleRequest()
        {
            var request = new AssumeRoleRequest
            {
                RoleArn = assumeRoleArn,
                RoleSessionName = assumeRoleSession,
                ExternalId = string.IsNullOrWhiteSpace(assumeRoleExternalId) ? null : assumeRoleExternalId
            };
            if (int.TryParse(assumeRoleDurationSeconds, out var durationSeconds))
                request.DurationSeconds = durationSeconds;

            return request;
        }
    }
}
