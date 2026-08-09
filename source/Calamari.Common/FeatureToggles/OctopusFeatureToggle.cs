using Calamari.Common.Plumbing.Variables;

namespace Calamari.Common.FeatureToggles
{
    public static class OctopusFeatureToggles
    {
        public static class KnownSlugs
        {
            public const string KustomizePatchImageUpdatesFeatureToggle = "kustomize-patch-image-updates";
            public const string ArgoRolloutsSupportFeatureToggle = "argo-rollouts-support";
            public const string UseDockerCredentialHelper = "calamari-use-docker-credential-helper";
            public const string GitDependenciesForScriptsFeatureToggle = "git-dependencies-for-scripts";
            public const string EnableLegacyKubernetesResourceChecks = "enable-legacy-kubernetes-resource-checks";
            public const string AzureWebAppIgnorePreservePathsFeatureToggle = "azure-web-app-ignore-preserve-paths";
            public const string AzureWebAppIgnoreChecksumFeatureToggle = "azure-web-app-ignore-checksum";
        };

        public static readonly OctopusFeatureToggle KustomizePatchImageUpdatesFeatureToggle = new(KnownSlugs.KustomizePatchImageUpdatesFeatureToggle);
        public static readonly OctopusFeatureToggle ArgoRolloutsSupportFeatureToggle = new(KnownSlugs.ArgoRolloutsSupportFeatureToggle);
        public static readonly OctopusFeatureToggle UseDockerCredentialHelperFeatureToggle = new(KnownSlugs.UseDockerCredentialHelper);
        public static readonly OctopusFeatureToggle GitDependenciesForScriptsFeatureToggle = new(KnownSlugs.GitDependenciesForScriptsFeatureToggle);
        public static readonly OctopusFeatureToggle EnableLegacyKubernetesResourceChecksFeatureToggle = new(KnownSlugs.EnableLegacyKubernetesResourceChecks);
        public static readonly OctopusFeatureToggle AzureWebAppIgnorePreservePathsFeatureToggle = new(KnownSlugs.AzureWebAppIgnorePreservePathsFeatureToggle);
        public static readonly OctopusFeatureToggle AzureWebAppIgnoreChecksumFeatureToggle = new(KnownSlugs.AzureWebAppIgnoreChecksumFeatureToggle);

        public class OctopusFeatureToggle
        {
            readonly string slug;

            public OctopusFeatureToggle(string slug)
            {
                this.slug = slug;
            }

            public bool IsEnabled(IVariables variables)
            {
                return variables.GetStrings(KnownVariables.EnabledFeatureToggles).Contains(slug);
            }
        }
    }
}
