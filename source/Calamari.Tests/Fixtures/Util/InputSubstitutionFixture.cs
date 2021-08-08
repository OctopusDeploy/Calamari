using Calamari.Util;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Tests.Fixtures.Util
{
    [TestFixture]
    public class InputSubstitutionFixture
    {
        [Test]
        public void VariablesInJsonInputsShouldBeEvaluated()
        {
            var variables = new CalamariVariables
            {
                { "Octopus.Action.Package[package].ExtractedPath", "C:\\OctopusTest\\Api Test\\1\\Octopus-Primary\\Work\\20210804020317-7-11\\package" },
            };
            var jsonInputs = "{\"containerNameOverride\":\"payload\",\"package\":{\"extractedToPath\":\"#{Octopus.Action.Package[package].ExtractedPath}\"},\"target\":{\"files\":[]}}";
            var evaluatedInputs = InputSubstitution.SubstituteAndEscapeAllVariablesInJson(jsonInputs, variables);

            var expectedEvaluatedInputs = "{\"containerNameOverride\":\"payload\",\"package\":{\"extractedToPath\":\"C:\\\\OctopusTest\\\\Api Test\\\\1\\\\Octopus-Primary\\\\Work\\\\20210804020317-7-11\\\\package\"},\"target\":{\"files\":[]}}";
            Assert.AreEqual(evaluatedInputs, expectedEvaluatedInputs);
        }
    }
}