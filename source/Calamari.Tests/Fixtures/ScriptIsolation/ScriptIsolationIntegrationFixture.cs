#nullable enable
using System.Threading.Tasks;
using Calamari.Testing.Helpers;
using Calamari.Tests.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.ScriptIsolation
{
    /// <summary>
    /// Integration tests that validate script isolation by launching real Calamari
    /// child processes with FullIsolation and the same mutex name, verifying that
    /// the scripts execute sequentially rather than concurrently.
    ///
    /// These tests cover the synchronous code path through
    /// <see cref="Calamari.Common.CalamariFlavourProgram"/> which calls
    /// <c>Isolation.Enforce()</c>.
    /// </summary>
    [TestFixture]
    public class ScriptIsolationIntegrationFixture : CalamariFixture
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

            var invocation1 = context.BuildInvocation(Calamari(), "Process1", mutexName, isolationLevel: "FullIsolation");
            var invocation2 = context.BuildInvocation(Calamari(), "Process2", mutexName, isolationLevel: "FullIsolation");

            var task1 = Task.Run(() => Invoke(invocation1));
            var task2 = Task.Run(() => Invoke(invocation2));

            var results = await Task.WhenAll(task1, task2);

            results[0].AssertSuccess();
            results[1].AssertSuccess();

            var entries = context.ParseTimestampLog();
            ScriptIsolationTestContext.AssertSequentialExecution(entries);
        }

        [Test]
        public async Task ConcurrentProcesses_WithMixedIsolation_RunSequentially()
        {
            var mutexName = ScriptIsolationTestContext.NewMutexName();

            var invocation1 = context.BuildInvocation(Calamari(), "Process1", mutexName, isolationLevel: "FullIsolation");
            var invocation2 = context.BuildInvocation(Calamari(), "Process2", mutexName, isolationLevel: "NoIsolation");

            var task1 = Task.Run(() => Invoke(invocation1));
            var task2 = Task.Run(() => Invoke(invocation2));

            var results = await Task.WhenAll(task1, task2);

            results[0].AssertSuccess();
            results[1].AssertSuccess();

            // FullIsolation acquires an exclusive lock (FileShare.None), which blocks
            // even a NoIsolation process trying to acquire a shared lock (FileShare.ReadWrite).
            var entries = context.ParseTimestampLog();
            ScriptIsolationTestContext.AssertSequentialExecution(entries);
        }

        [Test]
        public async Task ConcurrentProcesses_WithNoIsolation_CanRunConcurrently()
        {
            var mutexName = ScriptIsolationTestContext.NewMutexName();

            var invocation1 = context.BuildInvocation(Calamari(), "Process1", mutexName, isolationLevel: "NoIsolation");
            var invocation2 = context.BuildInvocation(Calamari(), "Process2", mutexName, isolationLevel: "NoIsolation");

            var task1 = Task.Run(() => Invoke(invocation1));
            var task2 = Task.Run(() => Invoke(invocation2));

            var results = await Task.WhenAll(task1, task2);

            results[0].AssertSuccess();
            results[1].AssertSuccess();

            // With NoIsolation (shared locks), both processes should have run.
            // We don't assert overlap since it's timing-dependent, but we verify
            // both completed successfully and both wrote their timestamps.
            var entries = context.ParseTimestampLog();
            ScriptIsolationTestContext.AssertBothProcessesWroteTimestamps(entries);
        }
    }
}
