#nullable enable
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Common.Features.Processes.ScriptIsolation;
using Calamari.Common.Plumbing.Commands;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using NUnit.Framework;
using Polly;
using Polly.Timeout;

namespace Calamari.Tests.Fixtures.ScriptIsolation
{
    [TestFixture]
    public class LockOptionsFixture
    {
        static string DefaultTentacleHome => OperatingSystem.IsWindows()
            ? @"C:\Octopus\Tentacle"
            : "/home/octopus/tentacle";

        static CommonOptions.ScriptIsolationOptions MakeOptions(
            string? level = "FullIsolation",
            string? mutexName = "TestMutex",
            string? timeout = "00:01:00",
            string? tentacleHome = null)
            => new()
            {
                Level = level,
                MutexName = mutexName,
                Timeout = timeout,
                TentacleHome = tentacleHome ?? DefaultTentacleHome
            };

        static CommonOptions.ScriptIsolationOptions MakeOptionsNoTentacleHome(
            string? level = "FullIsolation",
            string? mutexName = "TestMutex",
            string? timeout = "00:01:00")
            => new()
            {
                Level = level,
                MutexName = mutexName,
                Timeout = timeout,
                TentacleHome = null
            };

        [Test]
        public void FromScriptIsolationOptionsOrNull_ReturnsNull_WhenLevelIsNull()
        {
            var result = LockOptions.FromScriptIsolationOptionsOrNull(MakeOptions(level: null));
            result.Should().BeNull();
        }

        [Test]
        public void FromScriptIsolationOptionsOrNull_ReturnsNull_WhenLevelIsWhiteSpace()
        {
            var result = LockOptions.FromScriptIsolationOptionsOrNull(MakeOptions(level: "   "));
            result.Should().BeNull();
        }

        [Test]
        public void FromScriptIsolationOptionsOrNull_ReturnsNull_WhenMutexNameIsNull()
        {
            var result = LockOptions.FromScriptIsolationOptionsOrNull(MakeOptions(mutexName: null));
            result.Should().BeNull();
        }

        [Test]
        public void FromScriptIsolationOptionsOrNull_ReturnsNull_WhenMutexNameIsWhiteSpace()
        {
            var result = LockOptions.FromScriptIsolationOptionsOrNull(MakeOptions(mutexName: "   "));
            result.Should().BeNull();
        }

        [Test]
        public void FromScriptIsolationOptionsOrNull_ReturnsNull_WhenTentacleHomeIsMissing()
        {
            var result = LockOptions.FromScriptIsolationOptionsOrNull(MakeOptionsNoTentacleHome());
            result.Should().BeNull();
        }

        [Test]
        public void FromScriptIsolationOptionsOrNull_ReturnsNull_WhenTentacleHomeIsWhiteSpace()
        {
            var result = LockOptions.FromScriptIsolationOptionsOrNull(MakeOptions(tentacleHome: "   "));
            result.Should().BeNull();
        }

        [Test]
        public void FromScriptIsolationOptionsOrNull_MapsFullIsolationToExclusive()
        {
            var result = LockOptions.FromScriptIsolationOptionsOrNull(MakeOptions(level: "FullIsolation"));
            result.Should().NotBeNull();
            result.Type.Should().Be(LockType.Exclusive);
        }

        [Test]
        public void FromScriptIsolationOptionsOrNull_MapsNoIsolationToShared()
        {
            var result = LockOptions.FromScriptIsolationOptionsOrNull(MakeOptions(level: "NoIsolation"));
            result.Should().NotBeNull();
            result.Type.Should().Be(LockType.Shared);
        }

        [TestCase("fullisolation")]
        [TestCase("FULLISOLATION")]
        [TestCase("FullIsolation")]
        [TestCase("noisolation")]
        [TestCase("NOISOLATION")]
        [TestCase("NoIsolation")]
        public void FromScriptIsolationOptionsOrNull_IsCaseInsensitive(string isolationLevelValue)
        {
            var result = LockOptions.FromScriptIsolationOptionsOrNull(MakeOptions(level: isolationLevelValue));
            result.Should().NotBeNull();
        }

        [Test]
        public void FromScriptIsolationOptionsOrNull_ReturnsNull_ForUnknownIsolationLevel()
        {
            var result = LockOptions.FromScriptIsolationOptionsOrNull(MakeOptions(level: "SomeUnknownLevel"));
            result.Should().BeNull();
        }

        [Test]
        public void FromScriptIsolationOptionsOrNull_ParsesTimeout()
        {
            var result = LockOptions.FromScriptIsolationOptionsOrNull(MakeOptions(timeout: "00:05:00"));
            result.Should().NotBeNull();
            result.Timeout.Should().Be(TimeSpan.FromMinutes(5));
        }

        [Test]
        public void FromScriptIsolationOptionsOrNull_DefaultsToInfinite_WhenTimeoutIsInvalid()
        {
            var result = LockOptions.FromScriptIsolationOptionsOrNull(MakeOptions(timeout: "not-a-timespan"));
            result.Should().NotBeNull();
            result.Timeout.Should().Be(Timeout.InfiniteTimeSpan);
        }

        [Test]
        public void FromScriptIsolationOptionsOrNull_DefaultsToInfinite_WhenNoTimeoutIsSupplied()
        {
            var result = LockOptions.FromScriptIsolationOptionsOrNull(MakeOptions(timeout: string.Empty));
            result.Should().NotBeNull();
            result.Timeout.Should().Be(Timeout.InfiniteTimeSpan);
        }

        [Test]
        public void FromScriptIsolationOptionsOrNull_BuildsCorrectLockFilePath()
        {
            var scriptIsolationOptions = MakeOptions(mutexName: "MyMutex");
            var result = LockOptions.FromScriptIsolationOptionsOrNull(scriptIsolationOptions);
            result.Should().NotBeNull();
            var expectedLockFile = Path.Join(scriptIsolationOptions.TentacleHome, "ScriptIsolation.MyMutex.lock");

            result.LockFile.FullName.Should().Be(expectedLockFile);
        }

        [Test]
        public void FromScriptIsolationOptionsOrNull_Throws_WhenMutexNameContainsInvalidFileNameChar()
        {
            var invalidChar = Path.GetInvalidFileNameChars()[0];
            var badName = $"My{invalidChar}Mutex";

            Action act = () => LockOptions.FromScriptIsolationOptionsOrNull(MakeOptions(mutexName: badName));

            act.Should().Throw<ArgumentException>();
        }

        [Test]
        public void FromScriptIsolationOptionsOrNull_PreservesName()
        {
            var result = LockOptions.FromScriptIsolationOptionsOrNull(MakeOptions(mutexName: "MyMutex"));
            result.Should().NotBeNull();
            result.Name.Should().Be("MyMutex");
        }

        [Test]
        public async Task LockOptionsAcquisitionPipelineWithTimeoutGreaterThanOneDay_IsHandled()
        {
            var timeProvider = new FakeTimeProvider();
            var testCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var lockOptions = LockOptions.FromScriptIsolationOptionsOrNull(MakeOptions(timeout: "1.12:00:00"));
            lockOptions.Should().NotBeNull();
            var testPipelineBuilder = new ResiliencePipelineBuilder<ILockHandle>
            {
                TimeProvider = timeProvider
            };
            lockOptions.AddLockOptions(testPipelineBuilder);
            var testPipeline = testPipelineBuilder.Build();

            // Use a gate so we know when the pipeline has been entered at least once and
            // is about to wait on a fake-time delay (i.e., timers are registered).
            var pipelineEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            var pipelineTask = Task.Run(
                async () => await testPipeline.ExecuteAsync<ILockHandle>(
                    _ =>
                    {
                        pipelineEntered.TrySetResult();
                        throw new LockRejectedException("I never succeed");
                    },
                    testCts.Token),
                testCts.Token);

            // Wait until the pipeline callback has been invoked. After signalling, the
            // retry strategy still needs to schedule its first DelayAsync (which registers
            // a fake-time timer). Use a short real-time delay to allow the async pipeline
            // to fully reach its DelayAsync await before we start advancing fake time.
            await pipelineEntered.Task;
            await Task.Delay(TimeSpan.FromMilliseconds(50), testCts.Token);

            var startTime = timeProvider.GetUtcNow();
            var step = TimeSpan.FromMinutes(30);

            // Phase 1: advance to just before the expected timeout and assert the pipeline
            // is still running. This catches bugs where the timeout fires too early (e.g.
            // a hardcoded value shorter than lockOptions.Timeout).
            var beforeTimeout = lockOptions.Timeout - TimeSpan.FromMinutes(1);
            var elapsed = TimeSpan.Zero;
            while (elapsed < beforeTimeout)
            {
                // Cap each step so we never advance the clock past the beforeTimeout boundary.
                var advance = elapsed + step < beforeTimeout ? step : beforeTimeout - elapsed;
                timeProvider.Advance(advance);
                elapsed += advance;
                // Use a real-time delay (not just Task.Yield) so thread-pool continuations
                // from the pipeline have a full scheduling round to run and update IsCompleted.
                await Task.Delay(TimeSpan.FromMilliseconds(10), testCts.Token);
                if (pipelineTask.IsCompleted)
                    break;
            }

            pipelineTask.IsCompleted.Should().BeFalse("the pipeline should still be running before the timeout has elapsed");

            // Phase 2: advance past the expected timeout and assert the pipeline times out.
            var target = lockOptions.Timeout + TimeSpan.FromSeconds(1);
            while (elapsed < target && !pipelineTask.IsCompleted)
            {
                await Task.Yield();
                timeProvider.Advance(step);
                elapsed += step;
            }

            try
            {
                await pipelineTask;
            }
            catch (Exception e)
            {
                e.Should().BeOfType<TimeoutRejectedException>();
                timeProvider.GetUtcNow().Should().BeOnOrAfter(startTime + lockOptions.Timeout,
                    "the pipeline should not have timed out before the configured timeout elapsed");
                return;
            }

            Assert.Fail("Exception should have been thrown");
        }
    }
}
