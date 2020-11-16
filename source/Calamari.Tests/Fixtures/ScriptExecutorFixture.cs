using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Calamari.Tests.Fixtures
{
    class ScriptExecutorFixture
    {
        [Test]
        public void EnsureTimeoutSetForVariable()
        {
            var variables = new CalamariVariables();
            variables.Set(SpecialVariables.Action.Script.Timeout, "100");

            // the scripts typically coulb be C#, powershell, bash etc - we're only concerned that the
            // CommandLineInvocation picks up the variable, which is then used in the SilentProcessRunnerResult
            var script = new Script("script.ps1");
            var mockCommandLineRunner = new MockCommandLineRunner();

            var executor = new TestScriptExecutor();
            executor.Execute(
                script,
                variables,
                mockCommandLineRunner);

            mockCommandLineRunner.Invocations.Count().Should().Be(1);
            mockCommandLineRunner.Invocations.All(x => x.Timeout == TimeSpan.FromMilliseconds(100)).Should().BeTrue();
        }

        public class TestScriptExecutor : ScriptExecutor
        {
            protected override IEnumerable<ScriptExecution> PrepareExecution(
                Script script, 
                IVariables variables, 
                Dictionary<string, string> environmentVars = null)
            {
                yield return new ScriptExecution(
                    new CommandLineInvocation("program")
                    {
                        WorkingDirectory = TestContext.CurrentContext.TestDirectory,
                        EnvironmentVars = environmentVars
                    },
                    Enumerable.Empty<string>());
            }
        }

        public class MockCommandLineRunner : ICommandLineRunner
        {
            private readonly List<CommandLineInvocation> _invocations = new List<CommandLineInvocation>();

            public CommandResult Execute(CommandLineInvocation invocation)
            {
                _invocations.Add(invocation);
                return new CommandResult(invocation.Executable, 0);
            }

            public IEnumerable<CommandLineInvocation> Invocations => _invocations;
        }
    }
}
