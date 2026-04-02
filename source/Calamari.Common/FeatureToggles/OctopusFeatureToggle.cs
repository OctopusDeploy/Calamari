using Calamari.Common.Plumbing.Variables;

namespace Calamari.Common.FeatureToggles
{
    public static class OctopusFeatureToggles
    {
        public static class KnownSlugs
        {
            public const string AnsiColorsInTaskLogFeatureToggle = "ansi-colors";
            public const string ArgoCDHelmReplacePathFromContainerReferenceFeatureToggle = "argo-cd-helm-replace-path-from-container-reference";
            public const string KustomizePatchImageUpdatesFeatureToggle = "kustomize-patch-image-updates";
        };

        public static readonly OctopusFeatureToggle AnsiColorsInTaskLogFeatureToggle = new(KnownSlugs.AnsiColorsInTaskLogFeatureToggle);
        public static readonly OctopusFeatureToggle ArgoCDHelmReplacePathFromContainerReferenceFeatureToggle = new(KnownSlugs.ArgoCDHelmReplacePathFromContainerReferenceFeatureToggle);
        public static readonly OctopusFeatureToggle KustomizePatchImageUpdatesFeatureToggle = new(KnownSlugs.KustomizePatchImageUpdatesFeatureToggle);

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
