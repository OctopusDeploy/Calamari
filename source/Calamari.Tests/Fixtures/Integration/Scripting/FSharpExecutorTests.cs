using System;
using System.Collections.Generic;
using Calamari.Common.Commands;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Features.Scripting.FSharp;
using Calamari.Common.FeatureToggles;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Integration.Scripting
{
    [TestFixture]
    public class FSharpExecutorTests
    {
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

            var executor = new FSharpExecutor(log);

            // Act
            executor.Execute(script, variables, commandLineRunner);

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
        
        [Test]
        public void WhenDisableFSharpScriptExecutionFeatureToggleIsEnabled_ScriptExecutionThrowsAnException()
        {
            // Arrange
            var log = Substitute.For<ILog>();
            var script = new Script("fakeScript.fsx");
            var variables = new CalamariVariables
            {
                [KnownVariables.EnabledFeatureToggles] = FeatureToggle.DisableFSharpScriptExecutionFeatureToggle.ToString()
            };
            
            var commandResult = new CommandResult("fakeCommand", 0);
            var commandLineRunner = Substitute.For<ICommandLineRunner>();
            commandLineRunner.Execute(Arg.Any<CommandLineInvocation>()).Returns(commandResult);

            var executor = new FSharpExecutor(log);

            // Act
            Action act = () => executor.Execute(script, variables, commandLineRunner);

            // Assert
            act.Should().Throw<CommandException>();
        }
    }
}