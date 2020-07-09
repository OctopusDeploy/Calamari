using Calamari.Common.Features.StructuredVariables;
using Calamari.Variables;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.StructuredVariables
{
    [TestFixture]
    public class YamlVariableReplacerFixture : VariableReplacerFixture
    {
        public YamlVariableReplacerFixture() : base(new YamlFormatVariableReplacer())
        {
        }

        [Test]
        public void ShouldReplaceSimpleStringYamlVariables()
        {
            const string expected = @"environment: production
include:
- prod-system
- monitoring
";

            var variables = new CalamariVariables();
            variables.Set("environment", "production");
            variables.Set("include:0", "prod-system");

            var replaced = Replace(variables, "application-simple-string.yaml");

            AssertYamlEquivalent(replaced, expected);
        }

        void AssertYamlEquivalent(string replaced, string expected)
        {
            replaced.Should().Be(expected);
        }
    }
}