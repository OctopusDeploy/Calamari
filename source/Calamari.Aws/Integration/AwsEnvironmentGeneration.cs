using System;
using System.Collections.Specialized;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using Calamari.Integration.Processes;
using Newtonsoft.Json;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.Aws.Integration
{
    /// <summary>
    /// This service is used to generate the appropriate environment variables required to authentication
    /// custom scripts and the C# AWS SDK code exposed as Calamari commands that interact with AWS.
    /// </summary>
    public class AwsEnvironmentGeneration
    {
        private const string RoleUri = "http://169.254.169.254/latest/meta-data/iam/security-credentials/";
        private const string TentacleProxyHost = "TentacleProxyHost";
        private const string TentacleProxyPort = "TentacleProxyPort";
        private const string TentacleProxyUsername = "TentacleProxyUsername";
        private const string TentacleProxyPassword = "TentacleProxyPassword";
        private readonly string region;
        private readonly string accessKey;
        private readonly string secretKey;
        private readonly string assumeRole;
        private readonly string assumeRoleArn;
        private readonly string assumeRoleSession;

        public static async Task<AwsEnvironmentGeneration> Create(CalamariVariableDictionary variables)
        {
            var environmentGeneration = new AwsEnvironmentGeneration(variables);

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

        public StringDictionary EnvironmentVars { get; } = new StringDictionary();

        private AwsEnvironmentGeneration(CalamariVariableDictionary variables)
        {
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
            assumeRoleSession = variables.Get("Octopus.Action.Aws.AssumedRoleSession")?.Trim();
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
        public int ProxyPort => Environment.GetEnvironmentVariable(TentacleProxyPort)?
            .Map(val => Int32.TryParse(val, out var port) ? port : -1) ?? -1;
        public string ProxyHost => Environment.GetEnvironmentVariable(TentacleProxyHost);

        public ICredentials ProxyCredentials
        {
            get
            {
                var credentials = new NetworkCredential(
                    Environment.GetEnvironmentVariable(TentacleProxyUsername),
                    Environment.GetEnvironmentVariable(TentacleProxyPassword));

                return credentials.UserName != null && credentials.Password != null ? credentials : null;
            }
        }
        


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
                Log.Error("Error occured while verifying login");
                Log.Error(ex.Message);
                Log.Error(ex.StackTrace);
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
        /// <exception cref="LoginException">The supplied keys were not valid</exception>
        async Task PopulateSuppliedKeys()
        {
            if (!String.IsNullOrEmpty(accessKey))
            {
                EnvironmentVars["AWS_ACCESS_KEY_ID"] = accessKey;
                EnvironmentVars["AWS_SECRET_ACCESS_KEY"] = secretKey;
                if (!await VerifyLogin())
                {
                    throw new Exception("AWS-LOGIN-ERROR-0005: Failed to verify the credentials. " +
                                             "Please check the keys assigned to the Amazon Web Services Account associated with this step. " +
                                             "For more information visit https://g.octopushq.com/AwsCloudFormationDeploy#aws-login-error-0005");
                }
            }
        }

        /// <summary>
        /// If no keys were supplied, we must be using the instance role
        /// </summary>
        /// <exception cref="LoginException">The instance role information could not be extracted</exception>
        async Task PopulateKeysFromInstanceRole()
        {
            if (String.IsNullOrEmpty(accessKey))
            {
                try
                {
                    string payload;
                    using (var client = new HttpClient())
                    {
                        payload = await client.GetStringAsync(RoleUri);
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
                        "For more information visit https://g.octopushq.com/AwsCloudFormationDeploy#aws-login-error-0003", ex);
                }
            }
        }

        /// <summary>
        /// If we assume a secondary role, do it here
        /// </summary>
        async Task AssumeRole()
        {
            if ("True".Equals(assumeRole, StringComparison.InvariantCultureIgnoreCase))
            {
               var client = new AmazonSecurityTokenServiceClient(AwsCredentials);
               var credentials = (await client.AssumeRoleAsync(new AssumeRoleRequest
                   {
                       RoleArn = assumeRoleArn,
                       RoleSessionName = assumeRoleSession
                   })
               ).Credentials;

                EnvironmentVars["AWS_ACCESS_KEY_ID"] = credentials.AccessKeyId;
                EnvironmentVars["AWS_SECRET_ACCESS_KEY"] = credentials.SecretAccessKey;
                EnvironmentVars["AWS_SESSION_TOKEN"] = credentials.SessionToken;
            }
        }
    }
}