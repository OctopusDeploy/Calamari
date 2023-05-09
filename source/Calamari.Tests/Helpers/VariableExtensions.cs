using System.Linq;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;
using Calamari.FeatureToggles;

namespace Calamari.Tests.Helpers;

public static class VariableExtensions
{
    public static void SetFeatureToggles(this IVariables variables, params FeatureToggle[] featureToggles)
    {
        variables.Set(SpecialVariables.EnabledFeatureToggles,
            string.Join(",", featureToggles.Select(t => t.ToString())));
    }
}