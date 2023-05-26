using System;
using System.Linq;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;
using Calamari.FeatureToggles;

namespace Calamari.Tests.Helpers
{
    public static class VariableExtensions
    {
        public static void AddFeatureToggles(this IVariables variables, params FeatureToggle[] featureToggles)
        {
            var existingToggles = variables.Get(SpecialVariables.EnabledFeatureToggles)?.Split(',')
                                           .Select(Enum.Parse<FeatureToggle>) ?? Enumerable.Empty<FeatureToggle>();

            var allToggles = existingToggles.Concat(featureToggles).Distinct();

            variables.Set(SpecialVariables.EnabledFeatureToggles,
                string.Join(",", allToggles.Select(t => t.ToString())));
        }
    }
}