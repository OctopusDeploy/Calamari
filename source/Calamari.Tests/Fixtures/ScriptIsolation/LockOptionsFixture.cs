#nullable enable
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Common.Features.Processes.ScriptIsolation;
using Calamari.Common.Plumbing.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Testing.Helpers;
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
        string tempDir = null!;

        [SetUp]
        public void SetUp()
        {
            tempDir = Path.Join(Path.GetTempPath(), $"LockOptionsFixture.{Guid.NewGuid()}");
            Directory.CreateDirectory(tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }

        static string DefaultTentacleHome => Path.GetTempPath();

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

        static RequestedLockOptions? CreateRequestedOrNull(CommonOptions.ScriptIsolationOptions options)
            => new RequestedLockOptionsFactory(ConsoleLog.Instance).CreateFromIsolationOptions(options);

        static LockOptions? CreateLockOptionsOrNull(CommonOptions.ScriptIsolationOptions options)
        {
            var requested = CreateRequestedOrNull(options);
            if (requested is null)
                return null;
            return new LockOptionsResolver(new StubLockDirectoryFactory(), ConsoleLog.Instance).Create(requested);
        }

        /// <summary>
        /// Minimal stub that returns a fully-supported <see cref="LockDirectory"/> rooted at
        /// the supplied preferred directory, without performing any filesystem probing.
        /// </summary>
        sealed class StubLockDirectoryFactory : ILockDirectoryFactory
        {
            public LockDirectory Create(DirectoryInfo preferredLockDirectory)
                => new(preferredLockDirectory, LockCapability.Supported);
        }

        [Test]
        public void CreateRequestedOrNull_ReturnsNull_WhenLevelIsNull()
        {
            var result = CreateRequestedOrNull(MakeOptions(level: null));
            result.Should().BeNull();
        }

        [Test]
        public void CreateRequestedOrNull_ReturnsNull_WhenLevelIsWhiteSpace()
        {
            var result = CreateRequestedOrNull(MakeOptions(level: "   "));
            result.Should().BeNull();
        }

        [Test]
        public void CreateRequestedOrNull_ReturnsNull_WhenMutexNameIsNull()
        {
            var result = CreateRequestedOrNull(MakeOptions(mutexName: null));
            result.Should().BeNull();
        }

        [Test]
        public void CreateRequestedOrNull_ReturnsNull_WhenMutexNameIsWhiteSpace()
        {
            var result = CreateRequestedOrNull(MakeOptions(mutexName: "   "));
            result.Should().BeNull();
        }

        [Test]
        public void CreateRequestedOrNull_ReturnsNull_WhenTentacleHomeIsMissing()
        {
            var result = CreateRequestedOrNull(MakeOptionsNoTentacleHome());
            result.Should().BeNull();
        }

        [Test]
        public void CreateRequestedOrNull_ReturnsNull_WhenTentacleHomeIsWhiteSpace()
        {
            var result = CreateRequestedOrNull(MakeOptions(tentacleHome: "   "));
            result.Should().BeNull();
        }

        [Test]
        public void CreateRequestedOrNull_MapsFullIsolationToExclusive()
        {
            var result = CreateRequestedOrNull(MakeOptions(level: "FullIsolation"));
            result.Should().NotBeNull();
            result.Type.Should().Be(LockType.Exclusive);
        }

        [Test]
        public void CreateRequestedOrNull_MapsNoIsolationToShared()
        {
            var result = CreateRequestedOrNull(MakeOptions(level: "NoIsolation"));
            result.Should().NotBeNull();
            result.Type.Should().Be(LockType.Shared);
        }

        [TestCase("fullisolation")]
        [TestCase("FULLISOLATION")]
        [TestCase("FullIsolation")]
        [TestCase("noisolation")]
        [TestCase("NOISOLATION")]
        [TestCase("NoIsolation")]
        public void CreateRequestedOrNull_IsCaseInsensitive(string isolationLevelValue)
        {
            var result = CreateRequestedOrNull(MakeOptions(level: isolationLevelValue));
            result.Should().NotBeNull();
        }

        [Test]
        public void CreateRequestedOrNull_ReturnsNull_ForUnknownIsolationLevel()
        {
            var result = CreateRequestedOrNull(MakeOptions(level: "SomeUnknownLevel"));
            result.Should().BeNull();
        }

        [Test]
        public void CreateRequestedOrNull_ParsesTimeout()
        {
            var result = CreateRequestedOrNull(MakeOptions(timeout: "00:05:00"));
            result.Should().NotBeNull();
            result.Timeout.Should().Be(TimeSpan.FromMinutes(5));
        }

        [Test]
        public void CreateRequestedOrNull_DefaultsToInfinite_WhenTimeoutIsInvalid()
        {
            var result = CreateRequestedOrNull(MakeOptions(timeout: "not-a-timespan"));
            result.Should().NotBeNull();
            result.Timeout.Should().Be(Timeout.InfiniteTimeSpan);
        }

        [Test]
        public void CreateRequestedOrNull_DefaultsToInfinite_WhenNoTimeoutIsSupplied()
        {
            var result = CreateRequestedOrNull(MakeOptions(timeout: string.Empty));
            result.Should().NotBeNull();
            result.Timeout.Should().Be(Timeout.InfiniteTimeSpan);
        }

        [Test]
        public void CreateLockOptionsOrNull_BuildsCorrectLockFileName()
        {
            var scriptIsolationOptions = MakeOptions(mutexName: "MyMutex");
            var result = CreateLockOptionsOrNull(scriptIsolationOptions);
            result.Should().NotBeNull();

            result.LockFile.File.Name.Should().Be("ScriptIsolation.MyMutex.lock");
        }

        [Test]
        public void CreateRequestedOrNull_Throws_WhenMutexNameContainsInvalidFileNameChar()
        {
            var invalidChar = Path.GetInvalidFileNameChars()[0];
            var badName = $"My{invalidChar}Mutex";

            Action act = () => CreateRequestedOrNull(MakeOptions(mutexName: badName));

            act.Should().Throw<ArgumentException>();
        }

        [Test]
        public void CreateLockOptionsOrNull_PreservesName()
        {
            var result = CreateLockOptionsOrNull(MakeOptions(mutexName: "MyMutex"));
            result.Should().NotBeNull();
            result.Name.Should().Be("MyMutex");
        }

        [Test]
        public async Task LockOptionsAcquisitionPipelineWithTimeoutGreaterThanOneDay_IsHandled()
        {
            var timeProvider = new FakeTimeProvider();
            var testCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var lockOptions = CreateLockOptionsOrNull(MakeOptions(timeout: "1.12:00:00"));
            lockOptions.Should().NotBeNull();
            var testPipelineBuilder = new ResiliencePipelineBuilder<ILockHandle>
            {
                TimeProvider = timeProvider
            };
            var lockAcquisitionPipelineBuilder = new LockAcquisitionResiliencePipelineBuilder(testPipelineBuilder);
            var testPipeline = lockAcquisitionPipelineBuilder.AddLockOptions(lockOptions).Build();

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

        // -------------------------------------------------------------------------
        // LockOptionsResolver.DetermineActualLockTypeToUseBasedOnSupport tests
        // -------------------------------------------------------------------------

        // Builds a LockOptions with a LockDirectory that has the given capability.
        // Uses tempDir as the directory path so the path exists on disk.
        LockOptions MakeLockOptionsWithCapability(LockType type, LockCapability capability)
        {
            var dir = new LockDirectory(new DirectoryInfo(tempDir), capability);
            var lockFile = dir.GetLockFile("ScriptIsolation.TestMutex.lock");
            return new LockOptions(
                Type: type,
                Name: "TestMutex",
                LockFile: lockFile,
                Timeout: TimeSpan.FromMinutes(1));
        }

        static (LockOptions? result, InMemoryLog log) UseExclusiveIfSharedIsNotSupported(LockOptions opts)
        {
            var log = new InMemoryLog();
            var result = new LockOptionsResolver(new StubLockDirectoryFactory(), log).DetermineActualLockTypeToUseBasedOnSupport(opts);
            return (result, log);
        }

        [Test]
        public void LockOptionsFactory_ReturnsOriginal_WhenFullySupported()
        {
            // Supported capability + Exclusive → IsFullySupported = true → returned unchanged, no warning
            var opts = MakeLockOptionsWithCapability(LockType.Exclusive, LockCapability.Supported);

            var (result, log) = UseExclusiveIfSharedIsNotSupported(opts);

            result.Should().NotBeNull();
            result.Type.Should().Be(LockType.Exclusive);
            log.MessagesWarnFormatted.Should().BeEmpty();
        }

        [Test]
        public void LockOptionsFactory_ReturnsOriginal_WhenExclusiveOnlyAndExclusiveRequested()
        {
            // ExclusiveOnly + Exclusive → IsSupported = true (Exclusive is supported) → returned unchanged, no warning
            var opts = MakeLockOptionsWithCapability(LockType.Exclusive, LockCapability.ExclusiveOnly);

            var (result, log) = UseExclusiveIfSharedIsNotSupported(opts);

            result.Should().NotBeNull();
            result.Type.Should().Be(LockType.Exclusive);
            log.MessagesWarnFormatted.Should().BeEmpty();
        }

        [Test]
        public void LockOptionsFactory_PromotesToExclusive_WhenExclusiveOnlyAndSharedRequested()
        {
            // ExclusiveOnly + Shared → IsSupported = false; Supports(Exclusive) = true
            // → always promotes to Exclusive with a warning
            var opts = MakeLockOptionsWithCapability(LockType.Shared, LockCapability.ExclusiveOnly);

            var (result, log) = UseExclusiveIfSharedIsNotSupported(opts);

            result.Should().NotBeNull();
            result.Type.Should().Be(LockType.Exclusive,
                                    because: "shared lock should always be promoted to exclusive when shared locking is unavailable");
            log.MessagesWarnFormatted.Should().NotBeEmpty(
                because: "a warning should be issued when the lock type is promoted");
        }

        [Test]
        public void LockOptionsFactory_ReturnsNull_WhenUnsupportedAndExclusiveRequested()
        {
            // Unsupported + Exclusive → IsFullySupported = false, IsSupported = false,
            // Supports(Exclusive) = false → returns null and a warning
            var opts = MakeLockOptionsWithCapability(LockType.Exclusive, LockCapability.Unsupported);

            var (result, log) = UseExclusiveIfSharedIsNotSupported(opts);

            result.Should().BeNull(
                because: "no locking is supported at all so no lock should be acquired");
            log.MessagesWarnFormatted.Should().NotBeEmpty(
                because: "a warning should be issued when no isolation is available");
        }

        [Test]
        public void LockOptionsFactory_ReturnsNull_WhenUnsupportedAndSharedRequested()
        {
            // Unsupported + Shared → same as above
            var opts = MakeLockOptionsWithCapability(LockType.Shared, LockCapability.Unsupported);

            var (result, log) = UseExclusiveIfSharedIsNotSupported(opts);

            result.Should().BeNull(
                because: "no locking is supported at all so no lock should be acquired");
            log.MessagesWarnFormatted.Should().NotBeEmpty(
                because: "a warning should be issued when no isolation is available");
        }
    }
}
