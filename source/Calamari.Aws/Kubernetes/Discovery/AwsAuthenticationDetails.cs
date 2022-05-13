using System.Collections.Generic;
using Amazon.Runtime;
using Calamari.Common.Features.Discovery;

namespace Calamari.Kubernetes.Aws
{
    public class AwsAuthenticationDetails : ITargetDiscoveryAuthenticationDetails
    {
        const string DefaultSessionName = "OctopusKubernetesClusterDiscovery";
        public AWSCredentials ToCredentials()
        {
            var account = Credentials.Type == "account"
                ? new BasicAWSCredentials(Credentials.Account.AccessKey, Credentials.Account.SecretKey)
                : (AWSCredentials)new EnvironmentVariablesAWSCredentials();

            if (Role.Type == "assumeRole")
                return new AssumeRoleAWSCredentials(account,
                    Role.Arn,
                    Role.SessionName ?? DefaultSessionName,
                    new AssumeRoleAWSCredentialsOptions
                        { ExternalId = Role.ExternalId, DurationSeconds = Role.SessionDuration });

            return account;
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