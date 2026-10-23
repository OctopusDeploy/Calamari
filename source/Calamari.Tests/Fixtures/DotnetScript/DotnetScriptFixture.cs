using System;
using System.Collections;
using System.Collections.Generic;
using Calamari.Deployment;
using Calamari.Testing.Helpers;
using Calamari.Tests.Helpers;
using NUnit.Framework;
using System.Linq;

namespace Calamari.Tests.Fixtures.DotnetScript
{
    [TestFixture]
    [Category(TestCategory.ScriptingSupport.DotnetScript)]
    public class DotnetScriptFixture : CalamariFixture
    {
        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            var allEnvVars = Environment.GetEnvironmentVariables();

            //debugging
            var envVarOutput = (from DictionaryEntry entry in allEnvVars select $"{entry.Key}={entry.Value}").ToList();
            Console.WriteLine(string.Join(Environment.NewLine, envVarOutput));
        }

        [Test]
        public void ShouldPrintEncodedVariable()
        {
            var (output, _) = RunScript("PrintEncodedVariable.csx");

            output.AssertSuccess();
            output.AssertOutput("##octopus[setVariable name='RG9ua2V5' value='S29uZw==']");
        }

        [Test]
        public void ShouldPrintSensitiveVariable()
        {
            var (output, _) = RunScript("PrintSensitiveVariable.csx");

            output.AssertSuccess();
            output.AssertOutput("##octopus[setVariable name='UGFzc3dvcmQ=' value='Y29ycmVjdCBob3JzZSBiYXR0ZXJ5IHN0YXBsZQ==' sensitive='VHJ1ZQ==']");
        }

        [Test]
        public void ShouldCreateArtifact()
        {
            var (output, _) = RunScript("CreateArtifact.csx");

            output.AssertSuccess();
            output.AssertOutput("##octopus[createArtifact");
            output.AssertOutput("name='bXlGaWxlLnR4dA==' length='MTAw']");
        }

        [Test]
        public void ShouldUpdateProgress()
        {
            var (output, _) = RunScript("UpdateProgress.csx");

            output.AssertSuccess();
            output.AssertOutput("##octopus[progress percentage='NTA=' message='SGFsZiBXYXk=']");
        }

        [Test]
        public void ShouldCallHello()
        {
            var (output, _) = RunScript("Hello.csx",
                new Dictionary<string, string>()
                {
                    ["Name"] = "Paul",
                    ["Variable2"] = "DEF",
                    ["Variable3"] = "GHI",
                    ["Foo_bar"] = "Hello",
                    ["Host"] = "Never",
                });

            output.AssertSuccess();
            output.AssertOutput("Hello Paul");
            output.AssertOutput("This is dotnet script");
        }

        [Test]
        public void ShouldCallHelloWithSensitiveVariable()
        {
            var (output, _) = RunScript("Hello.csx",
                new Dictionary<string, string>()
                {
                    ["Name"] = "NameToEncrypt",
                },
                sensitiveVariablesPassword: "5XETGOgqYR2bRhlfhDruEg==");

            output.AssertSuccess();
            output.AssertOutput("Hello NameToEncrypt");
        }

        [Test]
        public void ShouldConsumeParametersWithQuotes()
        {
            var (output, _) = RunScript("Parameters.csx",
                new Dictionary<string, string>()
                {
                    [SpecialVariables.Action.Script.ScriptParameters] = "-- \"Para meter0\" Parameter1",
                });

            output.AssertSuccess();
            output.AssertOutput("Parameters Para meter0Parameter1");
        }

        [Test]
        public void ShouldConsumeParametersWithoutParametersPrefix()
        {
            var (output, _) = RunScript("Parameters.csx",
                new Dictionary<string, string>()
                {
                    [SpecialVariables.Action.Script.ScriptParameters] = "Parameter0 Parameter1",
                });

            output.AssertSuccess();
            output.AssertOutput("Parameters Parameter0Parameter1");
        }

        [TestCase(true)]
        [TestCase(false)]
        public void IsolatedLoadContext(bool disableIsolatedLoadContext)
        {
            var (output, _) = RunScript("IsolatedLoadContext.csx",
                new Dictionary<string, string>()
                {
                    [SpecialVariables.Action.Script.CSharp.BypassIsolation] = disableIsolatedLoadContext.ToString(),
                    [SpecialVariables.Action.Script.ScriptParameters] = "-- Parameter0 Parameter1",
                });
            output.AssertSuccess();
            output.AssertOutput("Parameters Parameter0Parameter1");

            //when isolated load context is disabled, the `NuGet.Commands` version will
            //be the version bundled with dotnet-script, not the version we defined in our script
            var expectedVersion = disableIsolatedLoadContext
                ? "6.14.3.1"
                : "6.10.1.5";

            output.AssertOutput($"NuGet.Commands version: {expectedVersion}");
        }

        [Test]
        public void LegacyIsolatedLoadContextFlag_IsStrippedAndDoesNotReachTheScriptsArguments()
        {
            var (output, _) = RunScript("IsolatedLoadContext.csx",
                new Dictionary<string, string>()
                {
                    [SpecialVariables.Action.Script.ScriptParameters] = "--isolated-load-context -- Parameter0 Parameter1",
                });

            output.AssertSuccess();
            output.AssertOutput("the isolated assembly load context is the default from dotnet-script 2.0 on, so the flag is redundant");
            output.AssertOutput("NuGet.Commands version: 6.10.1.5");
            output.AssertOutput("Parameters Parameter0Parameter1");
        }

        [Test]
        public void LegacyIsolatedLoadContextFlag_LogsVerboseMessageWhenBypassIsolationAlsoSet()
        {
            var (output, _) = RunScript("IsolatedLoadContext.csx",
                new Dictionary<string, string>()
                {
                    [SpecialVariables.Action.Script.CSharp.BypassIsolation] = bool.TrueString,
                    [SpecialVariables.Action.Script.ScriptParameters] = "--isolated-load-context -- Parameter0 Parameter1",
                });

            output.AssertSuccess();
            output.AssertOutput("also turns off the isolated assembly load context");
            output.AssertOutput("NuGet.Commands version: 6.14.3.1");
            output.AssertOutput("Parameters Parameter0Parameter1");
        }
    }
}