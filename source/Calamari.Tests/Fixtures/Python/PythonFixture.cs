using System.Collections.Generic;
using Calamari.Deployment;
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
        public void ShouldPrintSensitiveVariables()
        {
            var (output, _) = RunScript("printvariables.py", new Dictionary<string, string>
            {
                ["Variable1"] = "Secret",
                ["Variable2"] = "DEF",
                ["Variable3"] = "GHI",
                ["Foo_bar"] = "Hello",
                ["Host"] = "Never",
            }, sensitiveVariablesPassword: "5XETGOgqYR2bRhlfhDruEg==");  

            output.AssertSuccess();
            output.AssertOutput("V1=Secret");
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

        [Test]
        public void ShouldCaptureAllOutput()
        {
            var (output, _) = RunScript("output.py");
            output.AssertSuccess();
            output.AssertOutput("Hello!");
            output.AssertOutput("Hello verbose!");
            output.AssertOutput("Hello warning!");
        }

        [Test]
        public void ShouldConsumeParameters()
        {
            var (output, _) = RunScript("parameters.py", new Dictionary<string, string>
            {
                [SpecialVariables.Action.Script.ScriptParameters] = "parameter1 parameter2"
            });
            output.AssertSuccess();
            output.AssertOutput("Parameters parameter1 parameter2");
        }

    }
}