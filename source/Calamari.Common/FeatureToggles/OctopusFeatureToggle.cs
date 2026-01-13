using Calamari.Common.Plumbing.Variables;

namespace Calamari.Common.FeatureToggles
{
    public static class OctopusFeatureToggles
    {
        public static class KnownSlugs
        {
            public const string AnsiColorsInTaskLogFeatureToggle = "ansi-colors";
        };

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
