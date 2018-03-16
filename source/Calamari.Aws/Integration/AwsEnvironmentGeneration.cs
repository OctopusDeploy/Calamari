using System;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Text;
using Amazon;
using Amazon.Runtime;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using Calamari.Aws.Exceptions;
using Newtonsoft.Json;
using Octopus.CoreUtilities.Extensions;
using Octostache;

namespace Calamari.Aws.Integration
{
    /// <summary>
    /// This service is used to generate the appropiate environment variables required to authentication
    /// custom scripts and the C# AWS SDK code exposed as Calamari commands that interat with AWS.
    /// </summary>
    public class AwsEnvironmentGeneration : IAwsEnvironmentGeneration
    {
        private const string RoleUri = "http://169.254.169.254/latest/meta-data/iam/security-credentials/";
        private const string TentacleProxyHost = "TentacleProxyHost";
        private const string TentacleProxyPort = "TentacleProxyPort";
        private const string TentacleProxyUsername = "TentacleProxyUsername";
        private const string TentacleProxyPassword = "TentacleProxyPassword";
        private readonly string account;
        private readonly string region;
        private readonly string accessKey;
        private readonly string secretKey;
        private readonly string assumeRole;
        private readonly string assumeRoleArn;
        private readonly string assumeRoleSession;

        public StringDictionary EnvironmentVars { get; private set; }

        public AwsEnvironmentGeneration(VariableDictionary variables)
        {
            account = variables.Get("Octopus.Action.AwsAccount.Variable")?.Trim();
            region = variables.Get("Octopus.Action.Aws.Region")?.Trim();
            accessKey = variables.Get(account + ".AccessKey")?.Trim();
            secretKey = variables.Get(account + ".SecretKey")?.Trim();
            assumeRole = variables.Get("Octopus.Action.Aws.AssumeRole")?.Trim();
            assumeRoleArn = variables.Get("Octopus.Action.Aws.AssumedRoleArn")?.Trim();
            assumeRoleSession = variables.Get("Octopus.Action.Aws.AssumedRoleSession")?.Trim();
            EnvironmentVars = new StringDictionary();

            PopulateCommonSettings();
            PopulateSuppliedKeys();
            PopulateKeysFromInstanceRole();
            AssumeRole();
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
                var creds = new NetworkCredential(
                    Environment.GetEnvironmentVariable(TentacleProxyUsername),
                    Environment.GetEnvironmentVariable(TentacleProxyPassword));

                return creds.UserName != null && creds.Password != null ? creds : null;
            }
        }
        


        /// <summary>
        /// Verify that we can login with the supplied credentials
        /// </summary>
        /// <returns>true if login succeeds, false otherwise</returns>
        public bool VerifyLogin()
        {
            try
            {
                return new AmazonSecurityTokenServiceClient(AwsCredentials)
                    // Client becomes the response of the API call
                    .Map(client => client.GetCallerIdentity(new GetCallerIdentityRequest()))
                    // Any response is considered valid
                    .Map(response => true);
            }
            catch (AmazonServiceException ex)
            {
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
        void PopulateSuppliedKeys()
        {
            if (!String.IsNullOrEmpty(accessKey))
            {
                EnvironmentVars["AWS_ACCESS_KEY_ID"] = accessKey;
                EnvironmentVars["AWS_SECRET_ACCESS_KEY"] = secretKey;
                if (!VerifyLogin())
                {
                    throw new LoginException("AWS-LOGIN-ERROR-0005: Failed to verify the credentials. " +
                                             "Please check the keys assigned to the Amazon Web Services Account associated with this step. " +
                                             "For more information visit https://g.octopushq.com/AwsCloudFormationDeploy#aws-login-error-0005");
                }
            }
        }

        /// <summary>
        /// If no keys were supplied, we must be using the instance role
        /// </summary>
        /// <exception cref="LoginException">The instance role information could not be extracted</exception>
        void PopulateKeysFromInstanceRole()
        {
            if (String.IsNullOrEmpty(accessKey))
            {
                try
                {
                    var instanceRole = WebRequest
                        .Create(RoleUri)
                        .Map(request => request.GetResponse())
                        .Map(response => FunctionalExtensions.Using(
                            () => new StreamReader(response.GetResponseStream(), Encoding.UTF8),
                            stream => stream.ReadToEnd()));

                    dynamic instanceRoleKeys = WebRequest
                        .Create($"http://169.254.169.254/latest/meta-data/iam/security-credentials/{instanceRole}")
                        .Map(request => request.GetResponse())
                        .Map(response => FunctionalExtensions.Using(
                            () => new StreamReader(response.GetResponseStream(), Encoding.UTF8),
                            stream => stream.ReadToEnd()))
                        .Map(JsonConvert.DeserializeObject);

                    EnvironmentVars["AWS_ACCESS_KEY_ID"] = instanceRoleKeys.AccessKeyId;
                    EnvironmentVars["AWS_SECRET_ACCESS_KEY"] = instanceRoleKeys.SecretAccessKey;
                    EnvironmentVars["AWS_SESSION_TOKEN"] = instanceRoleKeys.Token;
                }
                catch (Exception ex)
                {
                    // This was either a generic error accessing the metadata URI, or accessing the
                    // dynamic properties resulted in an error (which means the response was not
                    // in the expected format).
                    throw new LoginException(
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
        void AssumeRole()
        {
            if ("True".Equals(assumeRole, StringComparison.InvariantCultureIgnoreCase))
            {
                var credentials = new AmazonSecurityTokenServiceClient(AwsCredentials)
                    // Client becomes the response of the API call
                    .Map(client => client.AssumeRole(new AssumeRoleRequest
                    {
                        RoleArn = assumeRoleArn,
                        RoleSessionName = assumeRoleSession
                    }))
                    // Get the credentials details from the response
                    .Map(response => response.Credentials);

                EnvironmentVars["AWS_ACCESS_KEY_ID"] = credentials.AccessKeyId;
                EnvironmentVars["AWS_SECRET_ACCESS_KEY"] = credentials.SecretAccessKey;
                EnvironmentVars["AWS_SESSION_TOKEN"] = credentials.SessionToken;
            }
        }
    }
}