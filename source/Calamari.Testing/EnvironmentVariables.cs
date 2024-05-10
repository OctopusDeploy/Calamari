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
        
        [EnvironmentVariable("Azure_OctopusAPITester_SubscriptionId", "op://Calamari Secrets for Tests/Azure - OctopusApiTester/subscription id")]
        AzureSubscriptionId,

        [EnvironmentVariable("Azure_OctopusAPITester_TenantId", "op://Calamari Secrets for Tests/Azure - OctopusApiTester/tenant id")]
        AzureSubscriptionTenantId,

        [EnvironmentVariable("Azure_OctopusAPITester_Password", "op://Calamari Secrets for Tests/Azure - OctopusApiTester/password")]
        AzureSubscriptionPassword,

        [EnvironmentVariable("Azure_OctopusAPITester_ClientId", "op://Calamari Secrets for Tests/Azure - OctopusApiTester/client application id")]
        AzureSubscriptionClientId,

        [EnvironmentVariable("Azure_OctopusAPITester_Certificate", "op://Calamari Secrets for Tests/Azure - OctopusApiTester/thumbprint")]
        AzureSubscriptionCertificate,

        [EnvironmentVariable("GitHub_OctopusAPITester_Username", "op://Calamari Secrets for Tests/GitHub Test Account/username")]
        GitHubUsername,

        [EnvironmentVariable("GitHub_OctopusAPITester_Password", "op://Calamari Secrets for Tests/GitHub Test Account/PAT")]
        GitHubPassword,

        [EnvironmentVariable("K8S_OctopusAPITester_Token", "op://Calamari Secrets for Tests/GKS Kubernetes API Test Cluster/Token")]
        KubernetesClusterToken,

        [EnvironmentVariable("K8S_OctopusAPITester_Server", "op://Calamari Secrets for Tests/GKS Kubernetes API Test Cluster/Server")]
        KubernetesClusterUrl,

        [EnvironmentVariable("Helm_OctopusAPITester_Password", "op://Calamari Secrets for Tests/Artifactory e2e-reader Test Account/password")]
        HelmPassword,

        [EnvironmentVariable("Artifactory_Admin_PAT", "op://Calamari Secrets for Tests/Artifactory Admin PAT/PAT")]
        ArtifactoryE2EPassword,

        [EnvironmentVariable("DockerHub_TestReaderAccount_Password", "op://Calamari Secrets for Tests/DockerHub Test Reader Account/password")]
        DockerReaderPassword,

        [EnvironmentVariable("AWS_E2E_AccessKeyId", "op://Calamari Secrets for Tests/AWS E2E Test User Keys/AccessKeyId")]
        AwsCloudFormationAndS3AccessKey,

        [EnvironmentVariable("AWS_E2E_SecretKeyId", "op://Calamari Secrets for Tests/AWS E2E Test User Keys/SecretKeyId")]
        AwsCloudFormationAndS3SecretKey,

        [EnvironmentVariable("CALAMARI_FEEDZV2URI", defaultValue: "https://f.feedz.io/octopus-deploy/integration-tests/nuget")]
        FeedzNuGetV2FeedUrl,

        [EnvironmentVariable("CALAMARI_FEEDZV3URI", defaultValue: "https://f.feedz.io/octopus-deploy/integration-tests/nuget/index.json")]
        FeedzNuGetV3FeedUrl,

        [EnvironmentVariable("CALAMARI_ARTIFACTORYV2URI", defaultValue: "https://nuget.packages.octopushq.com/")]
        ArtifactoryNuGetV2FeedUrl,

        [EnvironmentVariable("CALAMARI_ARTIFACTORYV3URI", defaultValue: "https://packages.octopushq.com/artifactory/api/nuget/v3/nuget")]
        ArtifactoryNuGetV3FeedUrl,

        [EnvironmentVariable("CALAMARI_AUTHURI", "op://Calamari Secrets For Tests/MyGet Package Manager/website")]
        MyGetFeedUrl,

        [EnvironmentVariable("CALAMARI_AUTHUSERNAME", "op://Calamari Secrets For Tests/MyGet Package Manager/username")]
        MyGetFeedUsername,

        [EnvironmentVariable("CALAMARI_AUTHPASSWORD", "op://Calamari Secrets For Tests/MyGet Package Manager/password")]
        MyGetFeedPassword,

        [EnvironmentVariable("GOOGLECLOUD_OCTOPUSAPITESTER_JSONKEY", "op://Calamari Secrets for Tests/Google Cloud Octopus Api Tester/jsonkey")]
        GoogleCloudJsonKeyfile,

        //TODO(tmm): not sure about this one - this is a copy of the github account above.
        [EnvironmentVariable("GitHub_RateLimitingPersonalAccessToken", "op://Calamari Secrets for Tests/GitHub Test Account/PAT")]
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
            return new SecretManagerClient(SecretManagerAccount, new[] { "op://Calamari Secrets for Tests/Azure - OctopusApiTester/subscription id" }, microsoftLogger);
        }

        public static void LogMissingVariables()
        {
            var missingVariables = Enum.GetValues(typeof(ExternalVariable))
                                       .Cast<ExternalVariable>()
                                       .Select(prop => EnvironmentVariableAttribute.Get(prop))
                                       .Where(attr => Environment.GetEnvironmentVariable(attr.Name) == null)
                                       .ToList();

            if (!missingVariables.Any())
                return;

            Log.Warn($"The following environment variables could not be found: " + $"\n{string.Join("\n", missingVariables.Select(var => $" - {var.Name}"))}" + $"\n\nTests that rely on these variables are likely to fail.");
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

                return attr.DefaultValue ?? 
                throw new Exception($"Unable to locate {attr.Name} as an environment variable, nor does its secretReference exist in the Octopus Secret Manager (1Password), and no default value is specified.");
            }

            return attr.DefaultValue
                   ?? throw new Exception($"Unable to locate {attr.Name} as an environment variable, the Secret Manager integrations is not currently enabled (enable via env var CALAMARI__Tests__SecretManagerEnabled), "
                                          + $"and no default value is specified.");
        }
    }

    [AttributeUsage(AttributeTargets.Field)]
    class EnvironmentVariableAttribute : Attribute
    {
        public string Name { get; }
        public string? SecretReference { get; }
        public string? DefaultValue { get; }

        public EnvironmentVariableAttribute(string name, string? secretReference = null, string? defaultValue = null)
        {
            Name = name;
            DefaultValue = defaultValue;
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