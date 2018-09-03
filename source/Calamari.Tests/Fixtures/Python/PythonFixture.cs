using System.Collections.Generic;
using Calamari.Tests.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Python
{
    public class PythonFixture : CalamariFixture
    {
        [Test]
        public void ShouldCallHello()
        {
            var (output, _) = RunScript("hello.py");

            output.AssertSuccess();
            output.AssertOutput("Hello");
        }

        [Test]
        public void ShouldPrintVariables()
        {
            var (output, _) = RunScript("printvariables.py", new Dictionary<string, string>
            {
                ["Variable1"] = "ABC",
                ["Variable2"] = "DEF",
                ["Variable3"] = "GHI",
                ["Foo_bar"] = "Hello",
                ["Host"] = "Never",
            });

            output.AssertSuccess();
            output.AssertOutput("V1=ABC");
            output.AssertOutput("V2=DEF");
            output.AssertOutput("V3=GHI");
            output.AssertOutput("Foo_bar=Hello");
        }

        [Test]
        public void ShouldSetVariables()
        {
            var (output, variables) = RunScript("setvariable.py");
            output.AssertSuccess();
            output.AssertOutput("##octopus[setVariable name='VGVzdEE=' value='V29ybGQh']");
            Assert.AreEqual("World!", variables.Get("TestA"));
        }
        
        [Test]
        public void ShouldWriteServiceMessageForArtifacts()
        {
            var (output, _) = RunScript("createartifact.py");
            output.AssertSuccess();
            output.AssertOutput("##octopus[createArtifact path='QzpcUGF0aFxGaWxlLnR4dA==' name='RmlsZS50eHQ=' length='MA==']");
        }
    }
}