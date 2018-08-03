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
using Calamari.Hooks;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
using Newtonsoft.Json;
using Octopus.CoreUtilities.Extensions;
using Octostache;

namespace Calamari.Aws.Integration
{
    /// <summary>
    /// This service is used to generate the appropiate environment variables required to authentication
    /// custom scripts and the C# AWS SDK code exposed as Calamari commands that interat with AWS.
    /// </summary>
    public class AwsEnvironmentGeneration : IAwsEnvironmentGeneration, IScriptWrapper
    {
        private const string RoleUri = "http://169.254.169.254/latest/meta-data/iam/security-credentials/";
        private const string TentacleProxyHost = "TentacleProxyHost";
        private const string TentacleProxyPort = "TentacleProxyPort";
        private const string TentacleProxyUsername = "TentacleProxyUsername";
        private const string TentacleProxyPassword = "TentacleProxyPassword";
        private StringDictionary envVars;
        private readonly string account;
        private readonly string region;
        private readonly string accessKey;
        private readonly string secretKey;
        private readonly string assumeRole;
        private readonly string assumeRoleArn;
        private readonly string assumeRoleSession;

        public StringDictionary EnvironmentVars
        {
            get
            {
                // Generate the vars when this getter is accessed rather than in the constructor.
                // Exceptions thrown in the constructor produce a mess of Autofac stack traces
                // that are impossible to decipher for end users.
                if (envVars == null)
                {
                    envVars = new StringDictionary();
                    PopulateCommonSettings(envVars);
                    PopulateSuppliedKeys(envVars);
                    PopulateKeysFromInstanceRole(envVars);
                    AssumeRole(envVars);
                }                
                return envVars;
            }
        }

        public AwsEnvironmentGeneration(CalamariVariableDictionary variables)
        {
            account = variables.Get("Octopus.Action.AwsAccount.Variable")?.Trim();
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
        void PopulateCommonSettings(StringDictionary envVars)
        {
            envVars["AWS_DEFAULT_REGION"] = region;
            envVars["AWS_REGION"] = region;
        }

        /// <summary>
        /// If the keys were explicitly supplied, use them directly
        /// </summary>
        /// <exception cref="LoginException">The supplied keys were not valid</exception>
        void PopulateSuppliedKeys(StringDictionary envVars)
        {
            if (!String.IsNullOrEmpty(accessKey))
            {
                envVars["AWS_ACCESS_KEY_ID"] = accessKey;
                envVars["AWS_SECRET_ACCESS_KEY"] = secretKey;
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
        void PopulateKeysFromInstanceRole(StringDictionary envVars)
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

                    envVars["AWS_ACCESS_KEY_ID"] = instanceRoleKeys.AccessKeyId;
                    envVars["AWS_SECRET_ACCESS_KEY"] = instanceRoleKeys.SecretAccessKey;
                    envVars["AWS_SESSION_TOKEN"] = instanceRoleKeys.Token;
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
        void AssumeRole(StringDictionary envVars)
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

                envVars["AWS_ACCESS_KEY_ID"] = credentials.AccessKeyId;
                envVars["AWS_SECRET_ACCESS_KEY"] = credentials.SecretAccessKey;
                envVars["AWS_SESSION_TOKEN"] = credentials.SessionToken;
            }
        }

        public bool Enabled { get; } = true;
        public IScriptWrapper NextWrapper { get; set; }

        public CommandResult ExecuteScript(Script script,
            ScriptSyntax scriptSyntax,
            CalamariVariableDictionary variables,
            ICommandLineRunner commandLineRunner,
            StringDictionary environmentVars)
        {
            return NextWrapper.ExecuteScript(
                script, scriptSyntax, 
                variables, 
                commandLineRunner,
                environmentVars.MergeDictionaries(EnvironmentVars));
        }
    }
}