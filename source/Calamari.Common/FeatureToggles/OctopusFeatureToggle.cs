using Calamari.Common.Plumbing.Variables;

namespace Calamari.Common.FeatureToggles
{
    public static class OctopusFeatureToggles
    {
        public static class KnownSlugs
        {
            public const string ArgoCDCreatePullRequestFeatureToggle = "argocd-create-pull-request";
            public const string UseDockerCredentialHelper = "calamari-use-docker-credential-helper";
            public const string DotNetScriptCompilationWarningFeatureToggle = "dotnet-script-compile-warning";
            public const string AnsiColorsInTaskLogFeatureToggle = "ansi-colors";
        };
        };

        public static readonly OctopusFeatureToggle ArgoCDCreatePullRequestFeatureToggle = new OctopusFeatureToggle(KnownSlugs.ArgoCDCreatePullRequestFeatureToggle);
        public static readonly OctopusFeatureToggle UseDockerCredentialHelperFeatureToggle = new OctopusFeatureToggle(KnownSlugs.UseDockerCredentialHelper);
        public static readonly OctopusFeatureToggle DotNetScriptCompilationWarningFeatureToggle = new OctopusFeatureToggle(KnownSlugs.DotNetScriptCompilationWarningFeatureToggle);

        public static readonly OctopusFeatureToggle AnsiColorsInTaskLogFeatureToggle = new OctopusFeatureToggle(KnownSlugs.AnsiColorsInTaskLogFeatureToggle);

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
