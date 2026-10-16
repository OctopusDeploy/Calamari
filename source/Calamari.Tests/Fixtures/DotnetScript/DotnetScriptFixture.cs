using System;
using System.Collections.Generic;
using Calamari.Common.Features.Scripting.DotnetScript;
using Calamari.Common.FeatureToggles;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;
using Calamari.Testing.Helpers;
using Calamari.Tests.Helpers;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.DotnetScript
{
    [TestFixture]
    [Category(TestCategory.ScriptingSupport.DotnetScript)]
    public class DotnetScriptFixture : CalamariFixture
    {
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

        /// <summary>
        /// IsolatedLoadContext.csx asks for NuGet.Commands 6.10.0.107, whose assemblies carry
        /// assembly version 6.10.1.5. dotnet-script loads NuGet itself to service #r "nuget:", so
        /// the version the script observes tells you which assembly load context won.
        ///
        /// Isolation on (the default from 2.0): the script's own closure loads in its own context and
        /// it sees the version it asked for. Isolation off: the script binds to whatever
        /// dotnet-script already has loaded in the default context - 6.14.3.1 in the 2.0.1 bundle.
        ///
        /// This test used to assert that isolation *off* fails outright, which held only because
        /// 1.6.0 happened to bundle NuGet 6.10.0.107 - a lower version than the script's 6.10.1.5,
        /// and the default context refuses a downgrade while accepting an upgrade. 2.0.1 bundles a
        /// higher version, so the same collision now resolves silently to the wrong assembly. The
        /// assertion is on the version binding rather than on a crash so that it keeps testing the
        /// load context rather than an accident of which version happens to be vendored.
        /// </summary>
        [Test]
        public void IsolatedAssemblyLoadContext_IsOnByDefault_SoAScriptGetsTheVersionItAskedFor()
        {
            var (output, _) = RunScript("IsolatedLoadContext.csx",
                                        new Dictionary<string, string>()
                                        {
                                            [SpecialVariables.Action.Script.ScriptParameters] = "-- Parameter0 Parameter1",
                                        });

            output.AssertSuccess();
            output.AssertOutput("NuGet.Commands version: 6.10.1.5");
            output.AssertOutput("Parameters Parameter0Parameter1");
        }

        [Test]
        public void DisableIsolatedLoadContext_BindsTheScriptToDotnetScriptsOwnCopy()
        {
            var (output, _) = RunScript("IsolatedLoadContext.csx",
                                        new Dictionary<string, string>()
                                        {
                                            [SpecialVariables.Action.Script.ScriptParameters] = "-- Parameter0 Parameter1",
                                            ["Octopus.Action.Script.CSharp.DisableIsolatedLoadContext"] = "true",
                                        });

            output.AssertSuccess();
            output.AssertOutput("NuGet.Commands version: 6.14.3.1");
            output.AssertOutput("Parameters Parameter0Parameter1");
        }

        /// <summary>
        /// 2.0 dropped --isolated-load-context, and dotnet-script forwards options it does not
        /// recognise into the script's own argument list instead of rejecting them. Left in place the
        /// flag would become Env.ScriptArgs[0] and shift every real argument along by one, so
        /// Calamari strips it. Isolation is the default now, so the customer still gets what they
        /// asked for.
        /// </summary>
        [Test]
        public void LegacyIsolatedLoadContextFlag_IsStrippedAndDoesNotReachTheScriptsArguments()
        {
            var (output, _) = RunScript("IsolatedLoadContext.csx",
                                        new Dictionary<string, string>()
                                        {
                                            [SpecialVariables.Action.Script.ScriptParameters] = "--isolated-load-context -- Parameter0 Parameter1",
                                        });

            output.AssertSuccess();
            output.AssertOutput("NuGet.Commands version: 6.10.1.5");
            output.AssertOutput("Parameters Parameter0Parameter1");
        }
    }
}