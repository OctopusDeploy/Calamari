using System;
using System.Linq;
using Calamari.Common.FeatureToggles;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Tests.Helpers
{
    public static class VariableExtensions
    {
        public static void AddFeatureToggles(this IVariables variables, params FeatureToggle[] featureToggles)
        {
            var existingToggles = variables.Get(KnownVariables.EnabledFeatureToggles)?.Split(',')
                                           .Select(t => (FeatureToggle)Enum.Parse(typeof(FeatureToggle), t)) ?? Enumerable.Empty<FeatureToggle>();

            var allToggles = existingToggles.Concat(featureToggles).Distinct();

            variables.Set(KnownVariables.EnabledFeatureToggles,
                string.Join(",", allToggles.Select(t => t.ToString())));
        }
    }
}