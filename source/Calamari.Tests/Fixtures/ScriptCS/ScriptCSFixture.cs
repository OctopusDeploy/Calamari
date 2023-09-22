using System;
using System.Collections.Generic;
using Calamari.Deployment;
using Calamari.Testing.Helpers;
using Calamari.Testing.Requirements;
using Calamari.Tests.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.ScriptCS
{
    [TestFixture]
    [Category(TestCategory.ScriptingSupport.ScriptCS)]
    public class ScriptCSFixture : CalamariFixture
    {
        [Test, RequiresDotNet45, RequiresMonoVersion400OrAbove, RequiresMonoVersionBefore(5, 14, 0)]
        public void ShouldPrintEncodedVariable()
        {
            var (output, _) = RunScript("PrintEncodedVariable.csx");

            output.AssertSuccess();
            output.AssertOutput("##octopus[setVariable name='RG9ua2V5' value='S29uZw==']");
        }

        [Test, RequiresDotNet45, RequiresMonoVersion400OrAbove, RequiresMonoVersionBefore(5, 14, 0)]
        public void ShouldPrintSensitiveVariable()
        {
            var (output, _) = RunScript("PrintSensitiveVariable.csx");

            output.AssertSuccess();
            output.AssertOutput("##octopus[setVariable name='UGFzc3dvcmQ=' value='Y29ycmVjdCBob3JzZSBiYXR0ZXJ5IHN0YXBsZQ==' sensitive='VHJ1ZQ==']");
        }

        [Test, RequiresDotNet45, RequiresMonoVersion400OrAbove, RequiresMonoVersionBefore(5, 14, 0)]
        public void ShouldCreateArtifact()
        {
            var (output, _) = RunScript("CreateArtifact.csx");

            output.AssertSuccess();
            output.AssertOutput("##octopus[createArtifact");
            output.AssertOutput("name='bXlGaWxlLnR4dA==' length='MTAw']");
        }

        [Test, RequiresDotNet45, RequiresMonoVersion400OrAbove, RequiresMonoVersionBefore(5, 14, 0)]
        public void ShouldUpdateProgress()
        {
            var (output, _) = RunScript("UpdateProgress.csx");

            output.AssertSuccess();
            output.AssertOutput("##octopus[progress percentage='NTA=' message='SGFsZiBXYXk=']");
        }

        [Test, RequiresDotNet45, RequiresMonoVersion400OrAbove, RequiresMonoVersionBefore(5, 14, 0)]
        public void ShouldCallHello()
        {
            var (output, _) = RunScript("Hello.csx", new Dictionary<string, string>()
            {
                ["Name"] = "Paul",
                ["Variable2"] = "DEF",
                ["Variable3"] = "GHI",
                ["Foo_bar"] = "Hello",
                ["Host"] = "Never",
            });

            output.AssertSuccess();
            output.AssertOutput("Hello Paul");
            output.AssertOutput("This is ScriptCS");
            output.AssertProcessNameAndId("scriptcs");
        }

        [Test, RequiresDotNet45, RequiresMonoVersion400OrAbove, RequiresMonoVersionBefore(5, 14, 0)]
        public void ShouldCallHelloWithSensitiveVariable()
        {
            var (output, _) = RunScript("Hello.csx", new Dictionary<string, string>()
                { ["Name"] = "NameToEncrypt" }, sensitiveVariablesPassword: "5XETGOgqYR2bRhlfhDruEg==");

            output.AssertSuccess();
            output.AssertOutput("Hello NameToEncrypt");
        }

        [Test, RequiresDotNet45]
        public void ShouldConsumeParametersWithQuotes()
        {
            var (output, _) = RunScript("Parameters.csx", new Dictionary<string, string>()
                { [SpecialVariables.Action.Script.ScriptParameters] = "-- \"Para meter0\" Parameter1" });

            output.AssertSuccess();
            output.AssertOutput("Parameters Para meter0Parameter1");
        }

        [Test, RequiresDotNet45]
        public void ShouldConsumeParametersWithoutParametersPrefix()
        {
            var (output, _) = RunScript("Parameters.csx", new Dictionary<string, string>()
                { [SpecialVariables.Action.Script.ScriptParameters] = "Parameter0 Parameter1" });

            output.AssertSuccess();
            output.AssertOutput("Parameters Parameter0Parameter1");
        }
    }
}