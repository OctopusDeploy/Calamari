using Assent;
using Calamari.Common.Features.StructuredVariables;
using Calamari.Tests.Helpers;
using Calamari.Variables;
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
            var variables = new CalamariVariables();
            variables.Set("server:ports:0", "8080");
            variables.Set("spring:h2:console:enabled", "false");
            variables.Set("environment", "production");

            var replaced = Replace(variables, "application.yaml");

            this.Assent(replaced, TestEnvironment.AssentConfiguration);
        }
    }
}