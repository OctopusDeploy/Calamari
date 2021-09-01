using System;

namespace Calamari.Tests.Shared
{
    public enum ExternalVariable
    {
        [EnvironmentVariable("AWS_OctopusAPITester_Access", "AWS - OctopusAPITester")]
        AwsAcessKey,

        [EnvironmentVariable("AWS_OctopusAPITester_Secret", "AWS - OctopusAPITester")]
        AwsSecretKey,

        [EnvironmentVariable("SSH_OctopusAPITester_PrivateKey", "SSH - OctopusAPITester")]
        SshPrivateKey,

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

        [EnvironmentVariable("Helm_OctopusAPITester_Password", "Helm Password for https://octopusdeploy.jfrog.io")]
        HelmPassword,

        [EnvironmentVariable("DockerHub_TestReaderAccount_Password", "Password for DockerHub Test reader account")]
        DockerReaderPassword,

        [EnvironmentVariable("AWS_E2E_AccessKeyId", "AWS E2E Test User Keys")]
        AwsCloudFormationAndS3AccessKey,

        [EnvironmentVariable("AWS_E2E_SecretKeyId", "AWS E2E Test User Keys")]
        AwsCloudFormationAndS3SecretKey
    }

    public static class ExternalVariables
    {
        public static string Get(ExternalVariable property)
        {
            var attr = EnvironmentVariableAttribute.Get(property);
            if (attr == null)
                throw new Exception($"`{property}` does not include a {nameof(EnvironmentVariableAttribute)}.");

            var valueFromEnv = Environment.GetEnvironmentVariable(attr.Name);
            if (valueFromEnv == null)
                throw new Exception($"Environment Variable `{attr.Name}` could not be found. The value can be found in the password store under `{attr.LastPassName}`");

            return valueFromEnv;
        }
    }

    [AttributeUsage(AttributeTargets.Field)]
    class EnvironmentVariableAttribute : Attribute
    {
        public EnvironmentVariableAttribute(string name, string lastPassName)
        {
            Name = name;
            LastPassName = lastPassName;
        }

        public string Name { get; }
        public string LastPassName { get; }

        public static EnvironmentVariableAttribute? Get(object enm)
        {
            var mi = enm?.GetType().GetMember(enm.ToString());
            if (mi == null || mi.Length <= 0)
                return null;

            return GetCustomAttribute(mi[0], typeof(EnvironmentVariableAttribute)) as EnvironmentVariableAttribute;
        }
    }
}