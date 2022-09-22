using System.Collections.Generic;
using Calamari.Deployment;
using Calamari.Testing.Helpers;
using Calamari.Testing.Requirements;
using Calamari.Tests.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Python
{
    public class PythonFixture : CalamariFixture
    {
        [Test, RequiresMinimumPython3Version(4)]
        public void ShouldCallHello()
        {
            var (output, _) = RunScript("hello.py");

            output.AssertSuccess();
            output.AssertOutput("Hello");
        }

        [Test, RequiresMinimumPython3Version(4)]
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

        [Test, RequiresMinimumPython3Version(4)]
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

        [Test, RequiresMinimumPython3Version(4)]
        public void ShouldSetVariables()
        {
            var (output, variables) = RunScript("setvariable.py");
            output.AssertSuccess();
            output.AssertOutput("##octopus[setVariable name='VGVzdEE=' value='V29ybGQh']");
            Assert.AreEqual("World!", variables.Get("TestA"));
        }
        
        [Test, RequiresMinimumPython3Version(4)]
        [Category(TestCategory.CompatibleOS.OnlyWindows)]
        public void ShouldWriteServiceMessageForArtifactsOnWindows()
        {
            var (output, _) = RunScript("createartifactwin.py");
            output.AssertSuccess();
            output.AssertOutput("##octopus[createArtifact path='QzpcUGF0aFxGaWxlLnR4dA==' name='RmlsZS50eHQ=' length='MA==']");
        }

        [Test, RequiresMinimumPython3Version(4)]
        [Category(TestCategory.CompatibleOS.OnlyNixOrMac)]
        public void ShouldWriteServiceMessageForArtifactsOnNix()
        {
            var (output, _) = RunScript("createartifactnix.py");
            output.AssertSuccess();
            output.AssertOutput("##octopus[createArtifact path='L2hvbWUvZmlsZS50eHQ=' name='ZmlsZS50eHQ=' length='MA==']");
        }
        
        [Test, RequiresMinimumPython3Version(4)]
        public void ShouldWriteUpdateProgress()
        {
            var (output, _) = RunScript("updateprogress.py");
            output.AssertSuccess();
            output.AssertOutput("##octopus[progress percentage='NTA=' message='SGFsZiBXYXk=']");
        }

        [Test, RequiresMinimumPython3Version(4)]
        public void ShouldCaptureAllOutput()
        {
            var (output, _) = RunScript("output.py");
            output.AssertSuccess();
            output.AssertOutput("Hello!");
            output.AssertOutput("Hello verbose!");
            output.AssertOutput("Hello warning!");
        }

        [Test, RequiresMinimumPython3Version(4)]
        public void ShouldConsumeParameters()
        {
            var (output, _) = RunScript("parameters.py", new Dictionary<string, string>
            {
                [SpecialVariables.Action.Script.ScriptParameters] = "parameter1 parameter2"
            });
            output.AssertSuccess();
            output.AssertOutput("Parameters parameter1 parameter2");
        }

        [Test, RequiresMinimumPython3Version(4)]
        public void ShouldFailStep()
        {
            var (output, _) = RunScript("failstep.py");

            output.AssertFailure();
        }

        [Test, RequiresMinimumPython3Version(4)]
        public void ShouldFailStepWithCustomMessage()
        {
            var (output, _) = RunScript("failstepwithmessage.py");

            output.AssertFailure();
            output.AssertOutput("##octopus[resultMessage message='Q3VzdG9tIGZhaWx1cmUgbWVzc2FnZQ==']");
        }
    }
}