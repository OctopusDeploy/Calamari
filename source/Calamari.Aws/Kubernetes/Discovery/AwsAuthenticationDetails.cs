using System;
using System.Collections.Generic;
using Amazon.Runtime;
using Calamari.Common.Features.Discovery;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.Aws.Kubernetes.Discovery
{
    public class AwsAuthenticationDetails : ITargetDiscoveryAuthenticationDetails
    {
        const string DefaultSessionName = "OctopusKubernetesClusterDiscovery";
        
        /// <exception cref="AggregateException">
        /// If both InstanceProfile and EnvironmentVariable Credentials fail.
        /// Contains AmazonClientExceptions for both InstanceProfile and EnvironmentVariable failures</exception>
        /// <exception cref="AmazonClientException">If Basic (Account) Credentials fail</exception>
        public bool TryGetCredentials(ILog log, out AWSCredentials credentials)
        {
            credentials = null;
            if (Credentials.Type == "account")
            {
                try
                {
                    credentials = new BasicAWSCredentials(Credentials.Account.AccessKey, Credentials.Account.SecretKey);
                }
                // Catching a generic Exception because AWS SDK throws undocumented exceptions.
                catch (Exception e)
                {
                    log.Warn("Unable to authorise credentials, see verbose log for details.");
                    log.Verbose($"Unable to authorise credentials for Account: {e}");
                    return false;
                }
            }
            else
            {
                // The sequence of fallbacks trying to log in with credentials exposed by the worker.
                // This follows the precedence document at https://docs.aws.amazon.com/cli/latest/userguide/cli-configure-quickstart.html#cli-configure-quickstart-precedence
                if (!(TryGetEnvironmentVariablesAwsCredentials(log, out credentials)
                      || TryGetAssumeRoleWithWebIdentityCredentials(log, out credentials)
                      || TryGetInstanceProfileAwsCredentials(log, out credentials)))
                {
                    log.Warn("Unable to authorise credentials, see verbose log for details.");
                    return false;
                }
            }

            if (Role.Type == "assumeRole")
            {
                credentials = new AssumeRoleAWSCredentials(credentials,
                    Role.Arn,
                    Role.SessionName ?? DefaultSessionName,
                    new AssumeRoleAWSCredentialsOptions
                        { ExternalId = Role.ExternalId, DurationSeconds = Role.SessionDuration });
            }
            
            return true;
        }

        bool TryGetAssumeRoleWithWebIdentityCredentials(ILog log, out AWSCredentials credentials)
        {
            try
            {
                credentials = AssumeRoleWithWebIdentityCredentials.FromEnvironmentVariables();
                return true;
            }
            catch(Exception ex)
            {
                log.Verbose($"Unable to authorise credentials for web identity: {ex}");
                credentials = null;
                return false;
            }
        }

        bool TryGetEnvironmentVariablesAwsCredentials(ILog log, out AWSCredentials credentials)
        {
            try
            {
                credentials = new EnvironmentVariablesAWSCredentials();
                return true;
            }
            catch(Exception ex)
            {
                log.Verbose($"Unable to authorise credentials for Environment Variables: {ex}");
                credentials = null;
                return false;
            }
        }

        bool TryGetInstanceProfileAwsCredentials(ILog log, out AWSCredentials credentials)
        {
            try
            {
                credentials = new InstanceProfileAWSCredentials();
                return true;
            }
            catch(Exception ex)
            {
                log.Verbose($"Unable to authorise credentials for Instance Profile: {ex}");
                credentials = null;
                return false;
            }
        }
        
        public string Type { get; set; }

        public AwsCredentials Credentials { get; set; }
        
        public AwsAssumedRole Role { get; set; }

        public IEnumerable<string> Regions { get; set; }
    }
    
    public class AwsAssumedRole
    {
        public string Type { get; set; }
            
        public string Arn { get; set; }
        
        public string SessionName { get; set; }
        
        public int? SessionDuration { get; set; }
        
        public string ExternalId { get; set; }
    }
    
    public class AwsCredentials
    {
        public string Type { get; set; }
            
        public string AccountId { get; set; }
            
        public AwsAccount Account { get; set; }
    }

    public class AwsAccount
    {
        public string AccessKey { get; set; }
        
        public string SecretKey { get; set; }
    }
}