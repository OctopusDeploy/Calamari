using System;
using System.Collections.Generic;
using Calamari.Common.Features.Scripting.DotnetScript;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;
using Calamari.Testing.Helpers;
using Calamari.Testing.Requirements;
using Calamari.Tests.Helpers;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.DotnetScript
{
    [TestFixture]
    [Category(TestCategory.ScriptingSupport.DotnetScript)]
    [RequiresDotNetCore]
    public class DotnetScriptFixture : CalamariFixture
    {
        static readonly Dictionary<string, string> RunWithDotnetScriptVariable = new Dictionary<string, string>() { { ScriptVariables.UseDotnetScript, bool.TrueString } };
        
        [Test]
        public void ShouldPrintEncodedVariable()
        {
            var (output, _) = RunScript("PrintEncodedVariable.csx", RunWithDotnetScriptVariable);

            output.AssertSuccess();
            output.AssertOutput("##octopus[setVariable name='RG9ua2V5' value='S29uZw==']");
        }

        [Test]
        public void ShouldPrintSensitiveVariable()
        {
            var (output, _) = RunScript("PrintSensitiveVariable.csx", RunWithDotnetScriptVariable);

            output.AssertSuccess();
            output.AssertOutput("##octopus[setVariable name='UGFzc3dvcmQ=' value='Y29ycmVjdCBob3JzZSBiYXR0ZXJ5IHN0YXBsZQ==' sensitive='VHJ1ZQ==']");
        }

        [Test]
        public void ShouldCreateArtifact()
        {
            var (output, _) = RunScript("CreateArtifact.csx", RunWithDotnetScriptVariable);

            output.AssertSuccess();
            output.AssertOutput("##octopus[createArtifact");
            output.AssertOutput("name='bXlGaWxlLnR4dA==' length='MTAw']");
        }

        [Test]
        public void ShouldUpdateProgress()
        {
            var (output, _) = RunScript("UpdateProgress.csx", RunWithDotnetScriptVariable);

            output.AssertSuccess();
            output.AssertOutput("##octopus[progress percentage='NTA=' message='SGFsZiBXYXk=']");
        }

        [Test]
        public void ShouldCallHello()
        {
            var (output, _) = RunScript("Hello.csx", new Dictionary<string, string>()
            {
                ["Name"] = "Paul",
                ["Variable2"] = "DEF",
                ["Variable3"] = "GHI",
                ["Foo_bar"] = "Hello",
                ["Host"] = "Never",
                [ScriptVariables.UseDotnetScript] = bool.TrueString
            });

            output.AssertSuccess();
            output.AssertOutput("Hello Paul");
            output.AssertOutput("This is dotnet script");
        }

        [Test]
        public void ShouldCallHelloWithSensitiveVariable()
        {
            var (output, _) = RunScript("Hello.csx", new Dictionary<string, string>()
            {
                ["Name"] = "NameToEncrypt",
                [ScriptVariables.UseDotnetScript] = bool.TrueString
            }, sensitiveVariablesPassword: "5XETGOgqYR2bRhlfhDruEg==");

            output.AssertSuccess();
            output.AssertOutput("Hello NameToEncrypt");
        }

        [Test]
        public void ShouldConsumeParametersWithQuotes()
        {
            var (output, _) = RunScript("Parameters.csx", new Dictionary<string, string>()
            {
                [SpecialVariables.Action.Script.ScriptParameters] = "-- \"Para meter0\" Parameter1",
                [ScriptVariables.UseDotnetScript] = bool.TrueString
            });

            output.AssertSuccess();
            output.AssertOutput("Parameters Para meter0Parameter1");
        }

        [Test]
        public void ShouldConsumeParametersWithoutParametersPrefix()
        {
            var (output, _) = RunScript("Parameters.csx", new Dictionary<string, string>()
            {
                [SpecialVariables.Action.Script.ScriptParameters] = "Parameter0 Parameter1",
                [ScriptVariables.UseDotnetScript] = bool.TrueString
            });

            output.AssertSuccess();
            output.AssertOutput("Parameters Parameter0Parameter1");
        }

        [TestCase(true)]
        [TestCase(false)]
        public void UsingIsolatedAssemblyLoadContext(bool enableIsolatedLoadContext)
        {
            var (output, _) = RunScript("IsolatedLoadContext.csx",
                                        new Dictionary<string, string>()
                                        {
                                            [SpecialVariables.Action.Script.ScriptParameters] = $"{(enableIsolatedLoadContext ? "--isolated-load-context " : "")}-- Parameter0 Parameter1",
                                            [ScriptVariables.UseDotnetScript] = bool.TrueString
                                        });
            if (enableIsolatedLoadContext)
            {
                output.AssertSuccess();
                output.AssertOutput("NuGet.Commands version: 6.10.0.");
                output.AssertOutput("Parameters Parameter0Parameter1");
            }
            else
            {
                output.AssertFailure();
                output.AssertErrorOutput("Could not load file or assembly 'NuGet.Protocol, Version=6.10.0.");
            }
        }

        [Test]
        public void HasInvalidSyntax_ShouldWriteExtraWarningLine()
        {
            var (output, _) = RunScript("InvalidSyntax.csx", new Dictionary<string, string>()
            {
                [ScriptVariables.UseDotnetScript] = bool.TrueString
            });

            output.CapturedOutput.AllMessages.Should().Contain(DotnetScriptCompilerWarningWrapper.WarningLogLine);

            //We are expecting failure
            output.AssertFailure();
        }
        
        [Test]
        public void ThrowsException_ShowNotWriteExtraWarningLine()
        {
            var (output, _) = RunScript("ThrowsException.csx", new Dictionary<string, string>()
            {
                [ScriptVariables.UseDotnetScript] = bool.TrueString
            });

            output.CapturedOutput.AllMessages.Should().NotContain(DotnetScriptCompilerWarningWrapper.WarningLogLine);
            //We are expecting failure
            output.AssertFailure();
        }
    }
}