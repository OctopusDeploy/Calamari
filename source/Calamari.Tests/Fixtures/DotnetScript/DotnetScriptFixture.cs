using System;
using System.Collections.Generic;
using Calamari.Deployment;
using Calamari.Testing.Helpers;
using Calamari.Testing.Requirements;
using Calamari.Tests.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.DotnetScript
{
    [TestFixture]
    [Category(TestCategory.ScriptingSupport.DotnetScript)]
    public class DotnetScriptFixture : CalamariFixture
    {
        [Test, RequiresDotNetCore]
        public void ShouldPrintEncodedVariable()
        {
            var (output, _) = RunScript("PrintEncodedVariable.csx");

            output.AssertSuccess();
            output.AssertOutput("##octopus[setVariable name='RG9ua2V5' value='S29uZw==']");
        }

        [Test, RequiresDotNetCore]
        public void ShouldPrintSensitiveVariable()
        {
            var (output, _) = RunScript("PrintSensitiveVariable.csx");

            output.AssertSuccess();
            output.AssertOutput("##octopus[setVariable name='UGFzc3dvcmQ=' value='Y29ycmVjdCBob3JzZSBiYXR0ZXJ5IHN0YXBsZQ==' sensitive='VHJ1ZQ==']");
        }

        [Test, RequiresDotNetCore]
        public void ShouldCreateArtifact()
        {
            var (output, _) = RunScript("CreateArtifact.csx");

            output.AssertSuccess();
            output.AssertOutput("##octopus[createArtifact");
            output.AssertOutput("name='bXlGaWxlLnR4dA==' length='MTAw']");
        }

        [Test, RequiresDotNetCore]
        public void ShouldUpdateProgress()
        {
            var (output, _) = RunScript("UpdateProgress.csx");

            output.AssertSuccess();
            output.AssertOutput("##octopus[progress percentage='NTA=' message='SGFsZiBXYXk=']");
        }

        [Test, RequiresDotNetCore]
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
            output.AssertProcessNameAndId("dotnet-script");
        }

        [Test, RequiresDotNetCore]
        public void ShouldCallHelloWithSensitiveVariable()
        {
            var (output, _) = RunScript("Hello.csx", new Dictionary<string, string>()
                { ["Name"] = "NameToEncrypt" }, sensitiveVariablesPassword: "5XETGOgqYR2bRhlfhDruEg==");

            output.AssertSuccess();
            output.AssertOutput("Hello NameToEncrypt");
        }

        [Test, RequiresDotNetCore]
        public void ShouldConsumeParametersWithQuotes()
        {
            var (output, _) = RunScript("Parameters.csx", new Dictionary<string, string>()
                { [SpecialVariables.Action.Script.ScriptParameters] = "-- \"Para meter0\" Parameter1" });

            output.AssertSuccess();
            output.AssertOutput("Parameters Para meter0Parameter1");
        }

        [Test, RequiresDotNetCore]
        public void ShouldConsumeParametersWithoutParametersPrefix()
        {
            var (output, _) = RunScript("Parameters.csx", new Dictionary<string, string>()
                { [SpecialVariables.Action.Script.ScriptParameters] = "Parameter0 Parameter1" });

            output.AssertSuccess();
            output.AssertOutput("Parameters Parameter0Parameter1");
        }
    }
}