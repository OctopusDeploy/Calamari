using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Common.Plumbing.Logging;
using Microsoft.Extensions.Logging;
using Octopus.OnePassword.Sdk;
using Serilog.Extensions.Logging;

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

        [EnvironmentVariable("GitHub_OctopusAPITester_Username", "GitHub Test Account", "op://Calamari Server Secrets for Tests/GitHub Test Account/username")]
        GitHubUsername,

        [EnvironmentVariable("GitHub_OctopusAPITester_Password", "GitHub Test Account", "op://Calamari Server Secrets for Tests/GitHub Test Account/PAT")]
        GitHubPassword,

        [EnvironmentVariable("K8S_OctopusAPITester_Token", "GKS Kubernetes API Test Cluster Token", "op://Calamari Server Secrets for Tests/GKS Kubernetes API Test Cluster/Token")]
        KubernetesClusterToken,

        [EnvironmentVariable("K8S_OctopusAPITester_Server", "GKS Kubernetes API Test Cluster Url", "op://Calamari Server Secrets for Tests/GKS Kubernetes API Test Cluster/Server")]
        KubernetesClusterUrl,

        [EnvironmentVariable("Helm_OctopusAPITester_Password", "Artifactory Test Account")]
        HelmPassword,

        [EnvironmentVariable("Artifactory_Admin_PAT", "Jfrog Artifactory Admin PAT", "op://Calamari Server Secrets for Tests/Artifactory Admin PAT/PAT")]
        ArtifactoryE2EPassword,

        [EnvironmentVariable("DockerHub_TestReaderAccount_Password", "DockerHub Test Reader Account", "op://Calamari Server Secrets for Tests/DockerHub Test Reader Account/password")]
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

        [EnvironmentVariable("GOOGLECLOUD_OCTOPUSAPITESTER_JSONKEY", "GoogleCloud - OctopusAPITester", "op://Calamari Secrets for Tests/Google Cloud Octopus Api Tester JsonKey")]
        GoogleCloudJsonKeyfile,
        
        [EnvironmentVariable("GitHub_RateLimitingPersonalAccessToken", "GitHub test account PAT")]
        GitHubRateLimitingPersonalAccessToken,
    }

    public static class ExternalVariables
    {
        static readonly Serilog.ILogger Logger = Serilog.Log.ForContext(typeof(ExternalVariables));
        
        static readonly bool SecretManagerIsEnabled = Convert.ToBoolean(Environment.GetEnvironmentVariable("CALAMARI__Tests__SecretManagerEnabled") ?? "True");
        static readonly string SecretManagerAccount = Environment.GetEnvironmentVariable("CALAMARI__Tests__SecretManagerAccount") ?? "octopusdeploy.1password.com";

        static readonly Lazy<SecretManagerClient> SecretManagerClient = new(LoadSecretManagerClient);
        
        static SecretManagerClient LoadSecretManagerClient()
        {
            var loggerFactory = new LoggerFactory();
            loggerFactory.AddProvider(new SerilogLoggerProvider(Logger, false));
            var microsoftLogger = loggerFactory.CreateLogger<SecretManagerClient>();
            return new SecretManagerClient(SecretManagerAccount, microsoftLogger);
        }
        
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

        public static async Task<string> Get(ExternalVariable property, CancellationToken cancellationToken)
        {
            var attr = EnvironmentVariableAttribute.Get(property);
            if (attr == null)
            {
                throw new Exception($"`{property}` does not include a {nameof(EnvironmentVariableAttribute)}.");
            }

            var valueFromEnv = Environment.GetEnvironmentVariable(attr.Name);
            if (valueFromEnv != null)
            {
                return valueFromEnv;
            }
            
            if (SecretManagerIsEnabled)
            {
                var valueFromSecretManager = string.IsNullOrEmpty(attr.SecretReference)
                    ? null
                    : await SecretManagerClient.Value.GetSecret(attr.SecretReference, cancellationToken, throwOnNotFound: false);
                if (!string.IsNullOrEmpty(valueFromSecretManager))
                {
                    return valueFromSecretManager;
                }
            }

            throw new Exception($"Unable to find `{attr.Name}` as either an Environment Variable, or a SecretReference. The value can be found in the password store under `{attr.LastPassName}`");
        }
    }

    [AttributeUsage(AttributeTargets.Field)]
    class EnvironmentVariableAttribute : Attribute
    {
        public string Name { get; }
        public string LastPassName { get; }
        public string? SecretReference { get; }
        public EnvironmentVariableAttribute(string name, string lastPassName, string? secretReference = null)
        {
            Name = name;
            LastPassName = lastPassName;
            SecretReference = secretReference;
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
