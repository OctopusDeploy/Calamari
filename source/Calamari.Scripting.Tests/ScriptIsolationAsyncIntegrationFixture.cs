#nullable enable
using System.Threading.Tasks;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing.Helpers;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Scripting.Tests
{
    /// <summary>
    /// Integration tests that validate script isolation by launching real Calamari.Scripting
    /// child processes with FullIsolation and the same mutex name, verifying that the scripts
    /// execute sequentially rather than concurrently.
    ///
    /// These tests cover the asynchronous code path through
    /// <see cref="Calamari.Common.CalamariFlavourProgramAsync"/> which calls
    /// <c>Isolation.EnforceAsync()</c>.
    /// </summary>
    [TestFixture]
    public class ScriptIsolationAsyncIntegrationFixture
    {
        ScriptIsolationTestContext context = null!;

        [SetUp]
        public void SetUp()
        {
            context = new ScriptIsolationTestContext();
        }

        [TearDown]
        public void TearDown()
        {
            context.Dispose();
        }

        [Test]
        public async Task ConcurrentProcesses_WithFullIsolation_RunSequentially()
        {
            var mutexName = ScriptIsolationTestContext.NewMutexName();

            var invocation1 = context.BuildInvocation(CreateBaseCommand(), "Process1", mutexName, isolationLevel: "FullIsolation");
            var invocation2 = context.BuildInvocation(CreateBaseCommand(), "Process2", mutexName, isolationLevel: "FullIsolation");

            var task1 = Task.Run(() => InvokeOutOfProcess(invocation1));
            var task2 = Task.Run(() => InvokeOutOfProcess(invocation2));

            var results = await Task.WhenAll(task1, task2);

            results[0].ExitCode.Should().Be(0,
                "Process1 should succeed. Output: " + results[0].CapturedOutput);
            results[1].ExitCode.Should().Be(0,
                "Process2 should succeed. Output: " + results[1].CapturedOutput);

            var entries = context.ParseTimestampLog();
            ScriptIsolationTestContext.AssertSequentialExecution(entries);
        }

        [Test]
        public async Task ConcurrentProcesses_WithMixedIsolation_RunSequentially()
        {
            var mutexName = ScriptIsolationTestContext.NewMutexName();

            var invocation1 = context.BuildInvocation(CreateBaseCommand(), "Process1", mutexName, isolationLevel: "FullIsolation");
            var invocation2 = context.BuildInvocation(CreateBaseCommand(), "Process2", mutexName, isolationLevel: "NoIsolation");

            var task1 = Task.Run(() => InvokeOutOfProcess(invocation1));
            var task2 = Task.Run(() => InvokeOutOfProcess(invocation2));

            var results = await Task.WhenAll(task1, task2);

            results[0].ExitCode.Should().Be(0,
                "Process1 should succeed. Output: " + results[0].CapturedOutput);
            results[1].ExitCode.Should().Be(0,
                "Process2 should succeed. Output: " + results[1].CapturedOutput);

            // FullIsolation acquires an exclusive lock (FileShare.None), which blocks
            // even a NoIsolation process trying to acquire a shared lock (FileShare.ReadWrite).
            var entries = context.ParseTimestampLog();
            ScriptIsolationTestContext.AssertSequentialExecution(entries);
        }

        [Test]
        public async Task ConcurrentProcesses_WithNoIsolation_CanRunConcurrently()
        {
            var mutexName = ScriptIsolationTestContext.NewMutexName();

            var invocation1 = context.BuildInvocation(CreateBaseCommand(), "Process1", mutexName, isolationLevel: "NoIsolation");
            var invocation2 = context.BuildInvocation(CreateBaseCommand(), "Process2", mutexName, isolationLevel: "NoIsolation");

            var task1 = Task.Run(() => InvokeOutOfProcess(invocation1));
            var task2 = Task.Run(() => InvokeOutOfProcess(invocation2));

            var results = await Task.WhenAll(task1, task2);

            results[0].ExitCode.Should().Be(0,
                "Process1 should succeed. Output: " + results[0].CapturedOutput);
            results[1].ExitCode.Should().Be(0,
                "Process2 should succeed. Output: " + results[1].CapturedOutput);

            // With NoIsolation (shared locks), both processes should have run.
            // We don't assert overlap since it's timing-dependent, but we verify
            // both completed successfully and both wrote their timestamps.
            var entries = context.ParseTimestampLog();
            ScriptIsolationTestContext.AssertBothProcessesWroteTimestamps(entries);
        }

        static CommandLine CreateBaseCommand()
        {
            var scriptingDllPath = typeof(Program).Assembly.Location;
            return new CommandLine(scriptingDllPath)
                .UseDotnet()
                .OutputToLog(false);
        }

        static CalamariResult InvokeOutOfProcess(CommandLine command)
        {
            var runner = new TestCommandLineRunner(ConsoleLog.Instance, new CalamariVariables());
            var result = runner.Execute(command.Build());
            return new CalamariResult(result.ExitCode, runner.Output);
        }
    }
}
