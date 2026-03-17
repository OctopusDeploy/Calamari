#nullable enable
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Common.Features.Processes.ScriptIsolation;
using Calamari.Common.Plumbing.Commands;
using Calamari.Testing.Helpers;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.ScriptIsolation
{
    [TestFixture]
    [Category(TestCategory.RunOnceOnWindowsAndLinux)]
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
    }
}
