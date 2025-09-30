using Calamari.Common.Plumbing.Variables;

namespace Calamari.Common.FeatureToggles
{
    public static class OctopusFeatureToggles
    {
        public static class KnownSlugs
        {
            public const string ArgoCDCreatePullRequestFeatureToggle = "argocd-create-pull-request";
            public const string DotNetScriptCompilationWarningFeatureToggle = "dotnet-script-compile-warning";
        };

        public static readonly OctopusFeatureToggle ArgoCDCreatePullRequestFeatureToggle = new OctopusFeatureToggle(KnownSlugs.ArgoCDCreatePullRequestFeatureToggle);
        public static readonly OctopusFeatureToggle DotNetScriptCompilationWarningFeatureToggle = new OctopusFeatureToggle(KnownSlugs.DotNetScriptCompilationWarningFeatureToggle);

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