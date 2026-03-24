#nullable enable
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Common.Features.Processes.ScriptIsolation;
using Calamari.Common.Plumbing.Commands;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.ScriptIsolation
{
    [TestFixture]
    public class IsolationFixture
    {
        string tempDir = null!;

        [SetUp]
        public void SetUp()
        {
            tempDir = Path.Join(Path.GetTempPath(), $"IsolationFixture.{Guid.NewGuid()}");
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

        CommonOptions.ScriptIsolationOptions FullIsolationOptions(
            string mutexName = "TestMutex",
            string timeout = "00:01:00")
        {
            return new CommonOptions.ScriptIsolationOptions
            {
                Level = "FullIsolation",
                MutexName = mutexName,
                Timeout = timeout,
                TentacleHome = tempDir
            };
        }

        CommonOptions.ScriptIsolationOptions NoIsolationOptions(
            string mutexName = "TestMutex",
            string timeout = "00:01:00")
        {
            return new CommonOptions.ScriptIsolationOptions
            {
                Level = "NoIsolation",
                MutexName = mutexName,
                Timeout = timeout,
                TentacleHome = tempDir
            };
        }

        [Test]
        public void Enforce_ReturnsNoLock_WhenOptionsAreEmpty()
        {
            // No Level, no MutexName — should silently return a no-op handle
            var emptyOptions = new CommonOptions.ScriptIsolationOptions();

            ILockHandle handle = null!;
            Action enforce = () => handle = Isolation.Enforce(emptyOptions);

            enforce.Should().NotThrow();
            handle.Should().NotBeNull();

            // No lock file should have been created
            var files = Directory.GetFiles(tempDir, "*.lock");
            files.Should().BeEmpty();
        }

        [Test]
        public void Enforce_AcquiresLock_WhenOptionsArePresent()
        {
            using var handle = Isolation.Enforce(FullIsolationOptions());

            handle.Should().NotBeNull();
            var expectedLockFile = Path.Join(tempDir, "ScriptIsolation.TestMutex.lock");
            File.Exists(expectedLockFile).Should().BeTrue();
        }

        [Test]
        public void Enforce_ReleasesLock_OnDispose()
        {
            var handle = Isolation.Enforce(FullIsolationOptions());
            handle.Dispose();

            // After releasing, a second Enforce call should succeed (exclusive lock is free)
            Action secondEnforce = () =>
            {
                using var secondHandle = Isolation.Enforce(FullIsolationOptions());
            };

            secondEnforce.Should().NotThrow();
        }

        [Test]
        public void Enforce_ThrowsLockRejectedException_WhenLockedExclusivelyByAnotherThread()
        {
            // Use a very short timeout so the test doesn't hang
            using var firstHandle = Isolation.Enforce(FullIsolationOptions(timeout: "00:00:00.010"));

            Action secondEnforce = () =>
            {
                using var secondHandle = Isolation.Enforce(FullIsolationOptions(timeout: "00:00:00.010"));
            };

            secondEnforce.Should().Throw<LockRejectedException>();
        }

        [Test]
        public void Enforce_AllowsConcurrentSharedLocks()
        {
            using var firstHandle = Isolation.Enforce(NoIsolationOptions());

            Action secondEnforce = () =>
            {
                using var secondHandle = Isolation.Enforce(NoIsolationOptions());
            };

            secondEnforce.Should().NotThrow();
        }

        [Test]
        public async Task EnforceAsync_AcquiresLock_WhenOptionsArePresent()
        {
            await using var handle = await Isolation.EnforceAsync(FullIsolationOptions(), CancellationToken.None);

            handle.Should().NotBeNull();
            var expectedLockFile = Path.Join(tempDir, "ScriptIsolation.TestMutex.lock");
            File.Exists(expectedLockFile).Should().BeTrue();
        }

        [Test]
        public async Task EnforceAsync_ReleasesLock_OnDisposeAsync()
        {
            var handle = await Isolation.EnforceAsync(FullIsolationOptions(), CancellationToken.None);
            await handle.DisposeAsync();

            Func<Task> secondEnforce = async () =>
            {
                await using var secondHandle = await Isolation.EnforceAsync(FullIsolationOptions(), CancellationToken.None);
            };

            await secondEnforce.Should().NotThrowAsync();
        }

        [Test]
        public async Task EnforceAsync_ThrowsLockRejectedException_WhenLockedExclusivelyByAnotherThread()
        {
            await using var firstHandle = await Isolation.EnforceAsync(FullIsolationOptions(timeout: "00:00:00.010"), CancellationToken.None);

            Func<Task> secondEnforce = async () =>
            {
                await using var secondHandle = await Isolation.EnforceAsync(FullIsolationOptions(timeout: "00:00:00.010"), CancellationToken.None);
            };

            await secondEnforce.Should().ThrowAsync<LockRejectedException>();
        }

        [Test]
        public async Task Enforce_WillWaitForLockToBeReleased()
        {
            var firstHandle = Isolation.Enforce(FullIsolationOptions(timeout: "00:00:00.010"));
            var t = Task.Run(() =>
                             {
                                 using var secondHandle = Isolation.Enforce(FullIsolationOptions(timeout: "00:00:00.500"));
                             }
                            );
            await Task.Delay(TimeSpan.FromMilliseconds(20));
            firstHandle.Dispose();
            await t;
        }

        [Test]
        public async Task EnforceAsync_WillWaitForLockToBeReleased()
        {
            var firstHandle = await Isolation.EnforceAsync(FullIsolationOptions(timeout: "00:00:00.500"), CancellationToken.None);
            var t = Task.Run(async () =>
                             {
                                 await using var secondHandle = await Isolation.EnforceAsync(FullIsolationOptions(timeout: "00:00:00.500"), CancellationToken.None);
                             }
                            );
            await Task.Delay(TimeSpan.FromMilliseconds(20));
            await firstHandle.DisposeAsync();
            await t;
        }

        /// <summary>
        /// Verifies that <see cref="Isolation.Enforce"/> honours its timeout when the lock is held
        /// by another thread for the duration.  A timeout in the range (10ms, 1 day) should trigger
        /// the Polly timeout strategy; without it the call would retry forever and this test would hang.
        /// </summary>
        [Test, Timeout(5000)]
        public void Enforce_ThrowsLockRejectedException_AfterTimeoutExpires_WhenLockIsHeld()
        {
            // Use a timeout that is:
            //   • > 10 ms  →  the infinite-retry / no-timeout branch is NOT taken; the Polly timeout
            //                 strategy must fire to stop retrying
            //   • Short enough that the test completes quickly
            const string lockTimeout = "00:00:00.200";

            // Acquire the exclusive lock on the current thread and hold it indefinitely.
            using var firstHandle = Isolation.Enforce(FullIsolationOptions(timeout: lockTimeout));

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            Action secondEnforce = () =>
            {
                using var secondHandle = Isolation.Enforce(FullIsolationOptions(timeout: lockTimeout));
            };

            // The second acquire must fail because the first lock is never released.
            secondEnforce.Should().Throw<LockRejectedException>();

            stopwatch.Stop();

            // The call should have given up at approximately the timeout (200 ms).
            // We allow generous headroom for slow CI machines, but cap it well below
            // the "retry forever" scenario — if the timeout is not enforced the test
            // would simply never return.
            stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10),
                because: "Enforce should stop retrying once the timeout elapses");
        }

        /// <summary>
        /// Async counterpart of <see cref="Enforce_ThrowsLockRejectedException_AfterTimeoutExpires_WhenLockIsHeld"/>.
        /// </summary>
        [Test, Timeout(5000)]
        public async Task EnforceAsync_ThrowsLockRejectedException_AfterTimeoutExpires_WhenLockIsHeld()
        {
            const string lockTimeout = "00:00:00.200";

            await using var firstHandle = await Isolation.EnforceAsync(FullIsolationOptions(timeout: lockTimeout), CancellationToken.None);

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            Func<Task> secondEnforce = async () =>
            {
                await using var secondHandle = await Isolation.EnforceAsync(FullIsolationOptions(timeout: lockTimeout), CancellationToken.None);
            };

            await secondEnforce.Should().ThrowAsync<LockRejectedException>();

            stopwatch.Stop();

            stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10),
                because: "EnforceAsync should stop retrying once the timeout elapses");
        }

        // -------------------------------------------------------------------------
        // ResolveLockOptions tests
        // -------------------------------------------------------------------------

        // Builds a LockOptions with a LockDirectory that has the given capability.
        // Uses tempDir as the directory path so the path exists on disk.
        static LockOptions MakeLockOptions(LockType type, LockCapability capability, string dirPath)
        {
            var dir = new LockDirectory(new DirectoryInfo(dirPath), capability);
            var lockFile = dir.GetLockFile("ScriptIsolation.TestMutex.lock");
            return new LockOptions(
                Type: type,
                Name: "TestMutex",
                LockFile: lockFile,
                Timeout: TimeSpan.FromMinutes(1));
        }

        [Test]
        public void ResolveLockOptions_ReturnsOriginal_WhenFullySupported()
        {
            // Supported capability + Exclusive → IsFullySupported = true → returned unchanged, no warning
            var opts = MakeLockOptions(LockType.Exclusive, LockCapability.Supported, tempDir);

            var result = Isolation.ResolveLockOptions(opts);

            result.Options.Should().BeSameAs(opts);
            result.Warning.Should().BeNull();
        }

        [Test]
        public void ResolveLockOptions_ReturnsOriginal_WhenExclusiveOnlyAndExclusiveRequested()
        {
            // ExclusiveOnly + Exclusive → IsSupported = true (Exclusive is supported) → returned unchanged, no warning
            var opts = MakeLockOptions(LockType.Exclusive, LockCapability.ExclusiveOnly, tempDir);

            var result = Isolation.ResolveLockOptions(opts);

            result.Options.Should().BeSameAs(opts);
            result.Warning.Should().BeNull();
        }

        [Test]
        public void ResolveLockOptions_PromotesToExclusive_WhenExclusiveOnlyAndSharedRequested()
        {
            // ExclusiveOnly + Shared → IsSupported = false; Supports(Exclusive) = true
            // → always promotes to Exclusive with a warning
            var opts = MakeLockOptions(LockType.Shared, LockCapability.ExclusiveOnly, tempDir);

            var result = Isolation.ResolveLockOptions(opts);

            result.Options.Should().NotBeNull();
            result.Options!.Type.Should().Be(LockType.Exclusive,
                because: "shared lock should always be promoted to exclusive when shared locking is unavailable");
            result.Warning.Should().NotBeNull(
                because: "a warning should be issued when the lock type is promoted");
        }

        [Test]
        public void ResolveLockOptions_ReturnsNull_WhenUnsupportedAndExclusiveRequested()
        {
            // Unsupported + Exclusive → IsFullySupported = false, IsSupported = false,
            // Supports(Exclusive) = false → returns null options and a warning
            var opts = MakeLockOptions(LockType.Exclusive, LockCapability.Unsupported, tempDir);

            var result = Isolation.ResolveLockOptions(opts);

            result.Options.Should().BeNull(
                because: "no locking is supported at all so no lock should be acquired");
            result.Warning.Should().NotBeNull(
                because: "a warning should be issued when no isolation is available");
        }

        [Test]
        public void ResolveLockOptions_ReturnsNull_WhenUnsupportedAndSharedRequested()
        {
            // Unsupported + Shared → same as above
            var opts = MakeLockOptions(LockType.Shared, LockCapability.Unsupported, tempDir);

            var result = Isolation.ResolveLockOptions(opts);

            result.Options.Should().BeNull(
                because: "no locking is supported at all so no lock should be acquired");
            result.Warning.Should().NotBeNull(
                because: "a warning should be issued when no isolation is available");
        }
    }
}
