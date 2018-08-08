using System.Collections.Generic;
using System.IO;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Calamari.Shared;
using Calamari.Tests.Helpers;
using NUnit.Framework;
using Octostache;

namespace Calamari.Tests.Fixtures.FSharp
{
    [TestFixture]
    [Category(TestEnvironment.ScriptingSupport.FSharp)]
    public class FSharpFixture : CalamariFixture
    {
        [Test, RequiresDotNet45, RequiresMonoVersion400OrAbove]
        public void ShouldPrintEncodedVariable()
        {
            var (output, _) = RunScript("OutputVariable.fsx");
            output.AssertSuccess();
            output.AssertOutput("##octopus[setVariable name='RG9ua2V5' value='S29uZw==']");
        }

        [Test, RequiresDotNet45, RequiresMonoVersion400OrAbove]
        public void ShouldPrintSensitiveVariable()
        {
            var (output, _) = RunScript("SensitiveOutputVariable.fsx", new Dictionary<string, string>()
                {["Name"] = null});

            output.AssertSuccess();
            output.AssertOutput(
                "##octopus[setVariable name='UGFzc3dvcmQ=' value='Y29ycmVjdCBob3JzZSBiYXR0ZXJ5IHN0YXBsZQ==' sensitive='VHJ1ZQ==']");
        }

        [Test, RequiresDotNet45, RequiresMonoVersion400OrAbove]
        public void ShouldCreateArtifact()
        {
            var (output, _) = RunScript("CreateArtifact.fsx", new Dictionary<string, string>()
                {["Name"] = null});

            output.AssertOutput("##octopus[createArtifact");
            output.AssertOutput("name='bXlGaWxlLnR4dA==' length='MTAw']");
        }

        [Test, RequiresDotNet45, RequiresMonoVersion400OrAbove]
        public void ShouldCallHello()
        {
            var (output, _) = RunScript("Hello.fsx", new Dictionary<string, string>()
            {
                ["Name"] = "Paul",
                ["Variable2"] = "DEF",
                ["Variable3"] = "GHI",
                ["Foo_bar"] = "Hello",
                ["Host"] = "Never",
            });

            output.AssertSuccess();
            output.AssertOutput("Hello Paul");
        }

        [Test, RequiresDotNet45, RequiresMonoVersion400OrAbove]
        public void ShouldCallHelloWithSensitiveVariable()
        {

            var (output, _) = RunScript("Hello.fsx",
                new Dictionary<string, string>() {["Name"] = "NameToEncrypt"},
                sensitiveVariablesPassword: "5XETGOgqYR2bRhlfhDruEg==");

            output.AssertSuccess();
            output.AssertOutput("Hello NameToEncrypt");
        }

        [Test, RequiresDotNet45, RequiresMonoVersion400OrAbove]
        public void ShouldCallHelloWithVariableSubstitution()
        {
            var (output, _) = RunScript("HelloVariableSubstitution.fsx", new Dictionary<string, string>()
                {["Name"] = "SubstitutedValue"});

            output.AssertSuccess();
            output.AssertOutput("Hello SubstitutedValue");
        }

        [Test, RequiresDotNet45, RequiresMonoVersion400OrAbove]
        public void ShouldCallHelloDirectValue()
        {
            var (output, _) = RunScript("Hello.fsx", new Dictionary<string, string>()
                {["Name"] = "direct value"});

            output.AssertSuccess();
            output.AssertOutput("Hello direct value");
        }

        [Test, RequiresDotNet45, RequiresMonoVersion400OrAbove]
        public void ShouldCallHelloDefaultValue()
        {
             var (output, _) = RunScript("HelloDefaultValue.fsx", new Dictionary<string, string>()
                {["Name"] = "direct value"});

            output.AssertSuccess();
            output.AssertOutput("Hello default value");
        }

        [Test, RequiresDotNet45, RequiresMonoVersion400OrAbove]
        public void ShouldCallHelloWithNullVariable()
        {
            var (output, _) = RunScript("Hello.fsx", new Dictionary<string, string>()
                {["Name"] = null});

            output.AssertSuccess();
            output.AssertOutput("Hello ");
        }

        [Test, RequiresDotNet45, RequiresMonoVersion400OrAbove]
        public void ShouldCallHelloWithNullSensitiveVariable()
        {
            var (output, _) = RunScript("Hello.fsx", new Dictionary<string, string>()
                {["Name"] = null}, sensitiveVariablesPassword: "5XETGOgqYR2bRhlfhDruEg==");

            output.AssertSuccess();
            output.AssertOutput("Hello ");
        }

        [Test, RequiresDotNet45, RequiresMonoVersion400OrAbove]
        public void ShouldConsumeParametersWithQuotes()
        {
            var (output, _) = RunScript("Parameters.fsx", new Dictionary<string, string>()
                { [SpecialVariables.Action.Script.ScriptParameters] = "\"Para meter0\" Parameter1" });

            output.AssertSuccess();
            output.AssertOutput("Parameters Para meter0-Parameter1");
        }
    }
}