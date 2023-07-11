using Calamari.Common.Plumbing.Variables;

namespace Calamari.Common.FeatureToggles
{
    public static class FeatureToggleExtensions
    {
        public static bool IsEnabled(this FeatureToggle featureToggle, IVariables variables)
        {
            var toggleName = featureToggle.ToString();
            return variables.GetStrings(KnownVariables.EnabledFeatureToggles).Contains(toggleName);
        }
    }
}