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
        public AWSCredentials GetCredentials()
        {
            var credentials = Credentials.Type == "account"
                ? new BasicAWSCredentials(Credentials.Account.AccessKey, Credentials.Account.SecretKey)
                : FallbackCredentialsFactory.GetCredentials();

            if (Role.Type == "assumeRole")
            {
                return new AssumeRoleAWSCredentials(credentials,
                    Role.Arn,
                    Role.SessionName ?? DefaultSessionName,
                    new AssumeRoleAWSCredentialsOptions
                        { ExternalId = Role.ExternalId, DurationSeconds = Role.SessionDuration });
            }

            return credentials;
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