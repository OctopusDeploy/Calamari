using System;
using System.Collections.Generic;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Features.Scripts;
using Calamari.Common.FeatureToggles;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing.Helpers;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Integration.Scripting
{
    [TestFixture]
    public class ScriptEngineFixture
    {
        static readonly ScriptSyntax[] ScriptPreferencesNonWindows =
        {
            ScriptSyntax.Bash,
            ScriptSyntax.Python,
            ScriptSyntax.CSharp,
            ScriptSyntax.FSharp,
            ScriptSyntax.PowerShell
        };

        static readonly ScriptSyntax[] ScriptPreferencesWindows =
        {
            ScriptSyntax.PowerShell,
            ScriptSyntax.Python,
            ScriptSyntax.CSharp,
            ScriptSyntax.FSharp,
            ScriptSyntax.Bash
        };

        static IEnumerable<TestCaseData> FSharpDeprecationFeatureToggleTestData()
        {
            yield return new TestCaseData(new KeyValuePair<string, string>(),
                                          false);
            yield return new TestCaseData(
                                          new KeyValuePair<string, string>(KnownVariables.EnabledFeatureToggles, ""),
                                          false);
            yield return new TestCaseData(
                                          new KeyValuePair<string, string>(KnownVariables.EnabledFeatureToggles, FeatureToggle.FSharpDeprecationFeatureToggle.ToString()),
                                          true);
        }

        [Test]
        [Category(TestCategory.CompatibleOS.OnlyWindows)]
        public void DeterminesCorrectScriptTypePreferenceOrderWindows()
        {
            DeterminesCorrectScriptTypePreferenceOrder(ScriptPreferencesWindows);
        }

        [Test]
        [Category(TestCategory.CompatibleOS.OnlyNixOrMac)]
        public void DeterminesCorrectScriptTypePreferencesOrderNonWindows()
        {
            DeterminesCorrectScriptTypePreferenceOrder(ScriptPreferencesNonWindows);
        }

        void DeterminesCorrectScriptTypePreferenceOrder(IEnumerable<ScriptSyntax> expected)
        {
            var engine = new ScriptEngine(new List<IScriptWrapper>(), Substitute.For<ILog>());
            var supportedTypes = engine.GetSupportedTypes();

            supportedTypes.Should().Equal(expected);
        }

        [Test]
        [TestCaseSource(nameof(FSharpDeprecationFeatureToggleTestData))]
        public void LogsFSharpDeprecationWarningWhenToggledOn(KeyValuePair<string, string> variableKvp, bool expected)
        {
            // Arrange
            var log = Substitute.For<ILog>();
            var script = new Script("fakeScript.fsx");
            var variables = new CalamariVariables
                { { variableKvp.Key, variableKvp.Value } };
            var commandResult = new CommandResult("fakeCommand", 0);
            var commandLineRunner = Substitute.For<ICommandLineRunner>();
            commandLineRunner.Execute(Arg.Any<CommandLineInvocation>()).Returns(commandResult);

            var engine = new ScriptEngine(new List<IScriptWrapper>(), log);

            // Act
            engine.Execute(script, variables, commandLineRunner);

            // Assert
            if (expected)
            {
                log.Received(1).Warn(Arg.Is<string>(s => s.StartsWith("Executing FSharp scripts will soon be deprecated")));
            }
            else
            {
                log.DidNotReceive().Warn(Arg.Any<string>());
            }
        }
    }
}