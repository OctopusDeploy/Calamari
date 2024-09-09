using Calamari.Common.Plumbing.Variables;

namespace Calamari.Common.FeatureToggles
{
    public static class OctopusFeatureToggles
    {
        public static readonly OctopusFeatureToggle NonPrimaryGitDependencySupportFeatureToggle = new OctopusFeatureToggle("non-primary-git-dependency-support");
        public static readonly OctopusFeatureToggle FullFrameworkTasksExternalProcess = new OctopusFeatureToggle("full-framework-tasks-external-process");
        
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