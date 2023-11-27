using System;
using System.Linq;
using Calamari.Common.Plumbing.Logging;
using NUnit.Framework;

namespace Calamari.Testing
{
    public enum ExternalVariable
    {
        [EnvironmentVariable("Azure_OctopusAPITester_SubscriptionId", "Azure - OctopusAPITester")]
        AzureSubscriptionId,

        [EnvironmentVariable("Azure_OctopusAPITester_TenantId", "Azure - OctopusAPITester")]
        AzureSubscriptionTenantId,

        [EnvironmentVariable("Azure_OctopusAPITester_Password", "Azure - OctopusAPITester")]
        AzureSubscriptionPassword,

        [EnvironmentVariable("Azure_OctopusAPITester_ClientId", "Azure - OctopusAPITester")]
        AzureSubscriptionClientId,

        [EnvironmentVariable("Azure_OctopusAPITester_Certificate", "Azure - OctopusAPITester")]
        AzureSubscriptionCertificate,

        [EnvironmentVariable("GitHub_OctopusAPITester_Username", "GitHub Test Account")]
        GitHubUsername,

        [EnvironmentVariable("GitHub_OctopusAPITester_Password", "GitHub Test Account")]
        GitHubPassword,

        [EnvironmentVariable("K8S_OctopusAPITester_Token", "GKS Kubernetes API Test Cluster Token")]
        KubernetesClusterToken,

        [EnvironmentVariable("K8S_OctopusAPITester_Server", "GKS Kubernetes API Test Cluster Url")]
        KubernetesClusterUrl,

        [EnvironmentVariable("Helm_OctopusAPITester_Password", "Artifactory Test Account")]
        HelmPassword,

        [EnvironmentVariable("ArtifactoryReader_OctopusAPITester_Password", "JFrog artifactory instance admin account")]
        ArtifactoryE2EPassword,

        [EnvironmentVariable("DockerHub_TestReaderAccount_Password", "DockerHub Test Reader Account")]
        DockerReaderPassword,

        [EnvironmentVariable("AWS_E2E_AccessKeyId", "AWS E2E Test User Keys")]
        AwsCloudFormationAndS3AccessKey,

        [EnvironmentVariable("AWS_E2E_SecretKeyId", "AWS E2E Test User Keys")]
        AwsCloudFormationAndS3SecretKey,

        [EnvironmentVariable("CALAMARI_FEEDZV2URI", "Not LastPass; Calamari TC Config Variables")]
        FeedzNuGetV2FeedUrl,

        [EnvironmentVariable("CALAMARI_FEEDZV3URI", "Not LastPass; Calamari TC Config Variables")]
        FeedzNuGetV3FeedUrl,
        
        [EnvironmentVariable("CALAMARI_ARTIFACTORYV2URI", "Not LastPass; Calamari TC Config Variables")]
        ArtifactoryNuGetV2FeedUrl,

        [EnvironmentVariable("CALAMARI_ARTIFACTORYV3URI", "Not LastPass; Calamari TC Config Variables")]
        ArtifactoryNuGetV3FeedUrl,
        
        [EnvironmentVariable("CALAMARI_AUTHURI", "OctopusMyGetTester")]
        MyGetFeedUrl,

        [EnvironmentVariable("CALAMARI_AUTHUSERNAME", "OctopusMyGetTester")]
        MyGetFeedUsername,

        [EnvironmentVariable("CALAMARI_AUTHPASSWORD", "OctopusMyGetTester")]
        MyGetFeedPassword,

        [EnvironmentVariable("GOOGLECLOUD_OCTOPUSAPITESTER_JSONKEY", "GoogleCloud - OctopusAPITester")]
        GoogleCloudJsonKeyfile,
        
        [EnvironmentVariable("GitHub_RateLimitingPersonalAccessToken", "GitHub test account PAT")]
        GitHubRateLimitingPersonalAccessToken,
    }

    public static class ExternalVariables
    {
        public static void LogMissingVariables()
        {
            var missingVariables = Enum.GetValues(typeof(ExternalVariable)).Cast<ExternalVariable>()
                                       .Select(prop => EnvironmentVariableAttribute.Get(prop))
                                       .Where(attr => Environment.GetEnvironmentVariable(attr.Name) == null)
                                       .ToList();

            if (!missingVariables.Any())
                return;

            Log.Warn($"The following environment variables could not be found: " +
                     $"\n{string.Join("\n", missingVariables.Select(var => $" - {var.Name}\t\tSource: {var.LastPassName}"))}" +
                     $"\n\nTests that rely on these variables are likely to fail.");
        }

        public static string Get(ExternalVariable property)
        {
            var attr = EnvironmentVariableAttribute.Get(property);
            if (attr == null)
            {
                throw new Exception($"`{property}` does not include a {nameof(EnvironmentVariableAttribute)}.");
            }

            var valueFromEnv = Environment.GetEnvironmentVariable(attr.Name);
            if (valueFromEnv == null)
            {
                throw new Exception($"Environment Variable `{attr.Name}` could not be found. The value can be found in the password store under `{attr.LastPassName}`");
            }

            return valueFromEnv;
        }
    }

    [AttributeUsage(AttributeTargets.Field)]
    class EnvironmentVariableAttribute : Attribute
    {
        public string Name { get; }
        public string LastPassName { get; }
        public EnvironmentVariableAttribute(string name, string lastPassName)
        {
            Name = name;
            LastPassName = lastPassName;
        }

        public static EnvironmentVariableAttribute? Get(object enm)
        {
            var mi = enm?.GetType().GetMember(enm.ToString());
            if (mi == null || mi.Length <= 0)
            {
                return null;
            }

            return GetCustomAttribute(mi[0], typeof(EnvironmentVariableAttribute)) as EnvironmentVariableAttribute;
        }
    }
}
