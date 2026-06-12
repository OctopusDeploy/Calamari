using System.Text.RegularExpressions;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Common.FeatureToggles
{
    public static class FeatureToggleExtensions
    {
        public static string ToSlug(this FeatureToggle featureToggle)
        {
            const string featureToggleSuffix = "FeatureToggle";
            
            var input = featureToggle.ToString();
            input = input.EndsWith(featureToggleSuffix)
                ? input[..^featureToggleSuffix.Length]
                : input;

            return Regex.Replace(input, @"(?<!^)(?=[A-Z])", "-").ToLowerInvariant();
        }

        public static bool IsEnabled(this FeatureToggle featureToggle, IVariables variables)
        {
            var toggleName = featureToggle.ToString();
            var toggleSlug = featureToggle.ToSlug();
            var enabledFeatureToggles = variables.GetStrings(KnownVariables.EnabledFeatureToggles);
            return enabledFeatureToggles.Contains(toggleName) || enabledFeatureToggles.Contains(toggleSlug);
        }
    }
}