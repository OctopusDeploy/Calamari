using Calamari.Common.FeatureToggles;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.Common.FeatureToggles
{
    [TestFixture]
    public class FeatureToggleExtensionsFixture
    {
        [Test]
        public void IsEnabled_GivenFeatureToggleNotInVariable_EvaluatesToFalsee()
        {
            var variables = GenerateVariableSet("FooFeatureToggle,BarFeatureToggle");
            FeatureToggle.SkunkworksFeatureToggle.IsEnabled(variables).Should().BeFalse();
        }

        [Test]
        public void IsEnabled_GivenFeatureToggleInVariable_EvaluatesToTrue()
        {
            var variables = GenerateVariableSet($"FooFeatureToggle,{FeatureToggle.SkunkworksFeatureToggle.ToString()},BarFeatureToggle");
            FeatureToggle.SkunkworksFeatureToggle.IsEnabled(variables).Should().BeTrue();
        }

        IVariables GenerateVariableSet(string variableValue)
        {
            var variables = new CalamariVariables();

            variables.Set(KnownVariables.EnabledFeatureToggles, variableValue);
            return variables;
        }
    }
}