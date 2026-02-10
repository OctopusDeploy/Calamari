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
            public const string Timeout = "Octopus.Action.Helm.Timeout";
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

        public static class Git
        {
            public static readonly string CommitMessageSummary = "Octopus.Action.ArgoCD.CommitMessageSummary";
            
            public static readonly string CommitMessageDescription = "Octopus.Action.ArgoCD.CommitMessageDescription";

            public static readonly string CommitMethod = "Octopus.Action.ArgoCD.CommitMethod";

            public static readonly string InputPath = "Octopus.Action.ArgoCD.InputPath";

            public static readonly string PurgeOutput = "Octopus.Action.ArgoCD.PurgeOutputFolder";
            
            public static class PullRequest
            {
                public static readonly string Create = "Octopus.Action.ArgoCD.PullRequest.Create";
            }

            public static class Output
            {
                public static readonly string GatewayIds = "ArgoCD.GatewayIds";
                public static readonly string GitUris = "ArgoCD.GitUris";
                public static readonly string MatchingApplications = "ArgoCD.MatchingApplications";
                public static readonly string MatchingApplicationTotalSourceCounts = "ArgoCD.MatchingApplicationTotalSourceCounts";
                public static readonly string MatchingApplicationMatchingSourceCounts = "ArgoCD.MatchingApplicationMatchingSourceCounts";
                public static readonly string UpdatedApplications = "ArgoCD.UpdatedApplications";
                public static readonly string UpdatedApplicationSourceCounts = "ArgoCD.UpdatedApplicationSourceCounts";
                public static readonly string UpdatedImages = "ArgoCD.UpdatedImages";

                public static ApplicationVariables Applications(string name) => new(name);
                
                public record ApplicationVariables(string Name)
                {
                    public ApplicationSourceVariables Sources(int index) => new (Name, index);

                    public static string Prefix(string name)
                    {
                        return $"ArgoCD.Applications[{name}]";
                    }

                    public record ApplicationSourceVariables(string ApplicationName, int Index)
                    {
                        public static string Prefix(int index) => $"Sources[{index}]";

                        public readonly string CommitSha = $"{ApplicationVariables.Prefix(ApplicationName)}.{ApplicationSourceVariables.Prefix(Index)}.CommitSha";
                        public readonly string PullRequestTitle = $"{ApplicationVariables.Prefix(ApplicationName)}.{ApplicationSourceVariables.Prefix(Index)}.PullRequest.Title";
                        public readonly string PullRequestNumber = $"{ApplicationVariables.Prefix(ApplicationName)}.{ApplicationSourceVariables.Prefix(Index)}.PullRequest.Number";
                        public readonly string PullRequestUrl = $"{ApplicationVariables.Prefix(ApplicationName)}.{ApplicationSourceVariables.Prefix(Index)}.PullRequest.Url";
                    }
                }
                
            }
        }

        public class ServiceMessages
        {
            public static class ResourceStatus
            {
                public const string Name = "k8s-status";

                public static class Attributes
                {
                    public const string Type = "type";
                    public const string ActionId = "actionId";
                    public const string StepName = "stepName";
                    public const string TaskId = "taskId";
                    public const string TargetId = "targetId";
                    public const string TargetName = "targetName";
                    public const string SpaceId = "spaceId";
                    public const string Uuid = "uuid";
                    public const string Group = "group";
                    public const string Version = "version";
                    public const string Kind = "kind";
                    public const string Name = "name";
                    public const string Namespace = "namespace";
                    public const string Status = "status";
                    public const string Data = "data";
                    public const string Removed = "removed";
                    public const string CheckCount = "checkCount";
                }
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
