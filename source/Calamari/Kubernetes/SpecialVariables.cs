using System;

namespace Calamari.Kubernetes
{
    public static class SpecialVariables
    {
        public const string ActionId = "Octopus.Action.Id";
        public const string ClusterUrl = "Octopus.Action.Kubernetes.ClusterUrl";
        public const string AksClusterName = "Octopus.Action.Kubernetes.AksClusterName";
        public const string AksClusterResourceGroup = "Octopus.Action.Kubernetes.AksClusterResourceGroup";
        public const string AksAdminLogin = "Octopus.Action.Kubernetes.AksAdminLogin";
        public const string EksClusterName = "Octopus.Action.Kubernetes.EksClusterName";
        public const string GkeClusterName = "Octopus.Action.Kubernetes.GkeClusterName";
        public const string GkeUseClusterInternalIp = "Octopus.Action.Kubernetes.GkeUseClusterInternalIp";
        public const string Namespace = "Octopus.Action.Kubernetes.Namespace";
        public const string SkipTlsVerification = "Octopus.Action.Kubernetes.SkipTlsVerification";
        public const string OutputKubeConfig = "Octopus.Action.Kubernetes.OutputKubeConfig";
        public const string CustomKubectlExecutable = "Octopus.Action.Kubernetes.CustomKubectlExecutable";
        public const string ResourceStatusCheck = "Octopus.Action.Kubernetes.ResourceStatusCheck";
        public const string DeploymentStyle = "Octopus.Action.KubernetesContainers.DeploymentStyle";
        public const string DeploymentWait = "Octopus.Action.KubernetesContainers.DeploymentWait";
        public const string CustomResourceYamlFileName = "Octopus.Action.KubernetesContainers.CustomResourceYamlFileName";
        public const string GroupedYamlDirectories = "Octopus.Action.KubernetesContainers.YamlDirectories";
        public const string KustomizeOverlayPath = "Octopus.Action.Kubernetes.Kustomize.OverlayPath";
        
        public const string Timeout = "Octopus.Action.Kubernetes.DeploymentTimeout";
        public const string WaitForJobs = "Octopus.Action.Kubernetes.WaitForJobs";
        public const string PrintVerboseKubectlOutputOnError = "Octopus.Action.Kubernetes.PrintVerboseKubectlOutputOnError";
        public const string ClientCertificate = "Octopus.Action.Kubernetes.ClientCertificate";
        public const string CertificateAuthorityPath = "Octopus.Action.Kubernetes.CertificateAuthorityPath";
        public const string PodServiceAccountTokenPath = "Octopus.Action.Kubernetes.PodServiceAccountTokenPath";
        public static string CertificatePem(string clientCertificate) => $"{clientCertificate}.CertificatePem";
        public static string PrivateKeyPem(string clientCertificate) => $"{clientCertificate}.PrivateKeyPem";
        public const string CertificateAuthority = "Octopus.Action.Kubernetes.CertificateAuthority";

        public const string KubeConfig = "Octopus.KubeConfig.Path";
        public const string KustomizeManifest = "Octopus.Kustomize.Manifest.Path";

        public const string ServerSideApplyEnabled = "Octopus.Action.Kubernetes.ServerSideApply.Enabled";
        public const string ServerSideApplyForceConflicts = "Octopus.Action.Kubernetes.ServerSideApply.ForceConflicts";
        
        public static class Helm
        {
            public const string ReleaseName = "Octopus.Action.Helm.ReleaseName";
            public const string Namespace = "Octopus.Action.Helm.Namespace";
            public const string KeyValues = "Octopus.Action.Helm.KeyValues";
            public const string YamlValues = "Octopus.Action.Helm.YamlValues";
            public const string ResetValues = "Octopus.Action.Helm.ResetValues";
            public const string TemplateValuesSources = "Octopus.Action.Helm.TemplateValuesSources";
            public const string AdditionalArguments = "Octopus.Action.Helm.AdditionalArgs";
            public const string CustomHelmExecutable = "Octopus.Action.Helm.CustomHelmExecutable";
            public const string ClientVersion = "Octopus.Action.Helm.ClientVersion";
            public const string Timeout = "Octopus.Action.Helm.Timeout";
            public const string TillerNamespace = "Octopus.Action.Helm.TillerNamespace";
            public const string TillerTimeout = "Octopus.Action.Helm.TillerTimeout";
            public const string ChartDirectory = "Octopus.Action.Helm.ChartDirectory";

            public static class Packages
            {
                public const string CustomHelmExePackageKey = "HelmExe";
                public static string ValuesFilePath(string key)
                {
                    return $"Octopus.Action.Package[{key}].ValuesFilePath";
                }
            }
        }

        public class ServiceMessageNames
        {
            public static class ResourceStatus
            {
                public const string Name = "k8s-status";
            }
            
            public static class ManifestApplied
            {
                public const string Name = "k8s-manifest-applied";
                public const string ManifestAttribute = "manifest";
                public const string NamespaceAttribute = "ns";
            }
        }
    }
}
