using Calamari.Common.Plumbing.Variables;

namespace Calamari.Common.FeatureToggles
{
    public static class OctopusFeatureToggles
    {
        public static class KnownSlugs
        {
            public const string KubernetesObjectManifestInspection = "kubernetes-object-manifest-inspection";
        };

        public static readonly OctopusFeatureToggle NonPrimaryGitDependencySupportFeatureToggle = new OctopusFeatureToggle("non-primary-git-dependency-support");
        public static readonly OctopusFeatureToggle KubernetesObjectManifestInspectionFeatureToggle = new OctopusFeatureToggle(KnownSlugs.KubernetesObjectManifestInspection);

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