using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;

namespace Calamari.FeatureToggles
{
    public static class FeatureToggleExtensions
    {
        public static bool IsEnabled(this FeatureToggle featureToggle, IVariables variables)
        {
            var toggleName = featureToggle.ToString();
            return variables.GetStrings(SpecialVariables.EnabledFeatureToggles).Contains(toggleName);
        }
    }
}