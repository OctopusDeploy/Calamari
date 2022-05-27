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
                try
                {
                    // If not currently running on an EC2 instance,
                    // this will throw an exception.
                    credentials = new InstanceProfileAWSCredentials();
                }
                // Catching a generic Exception because AWS SDK throws undocumented exceptions.
                catch (Exception instanceProfileException)
                {
                    try
                    {
                        // The last attempt is trying to use Environment Variables.
                        credentials = new EnvironmentVariablesAWSCredentials();
                    }
                    // Catching a generic Exception because AWS SDK throws undocumented exceptions.
                    catch (Exception environmentVariablesException)
                    {
                        log.Warn("Unable to authorise credentials, see verbose log for details.");
                        log.Verbose($"Unable to authorise credentials for Instance Profile: {instanceProfileException}");
                        log.Verbose($"Unable to authorise credentials for Environment Variables: {environmentVariablesException}");
                        return false;
                    }
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