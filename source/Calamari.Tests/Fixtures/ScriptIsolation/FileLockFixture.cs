using System;
using System.IO;
using System.Threading.Tasks;
using Calamari.Common.Features.Processes.ScriptIsolation;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.ScriptIsolation
{
    [TestFixture]
    public class FileLockFixture
    {
        LockFile lockFilePath = null!;

        [SetUp]
        public void SetUp()
        {
            lockFilePath = new LockDirectory(new DirectoryInfo(Path.GetTempPath()), LockCapability.Supported)
                .GetLockFile($"ScriptIsolation.FileLockFixture.{Guid.NewGuid():N}.lock");
        }

        [TearDown]
        public void TearDown()
        {
            lockFilePath?.Delete();
        }

        LockOptions MakeLockOptions(LockType lockType) => new(lockType, "TestMutex", lockFilePath, TimeSpan.FromSeconds(5));

        [Test]
        public void Acquire_ReturnsLockHandle_ForSharedLock()
        {
            using var handle = FileLock.Acquire(MakeLockOptions(LockType.Shared));

            handle.Should().NotBeNull();
            lockFilePath.Exists.Should().BeTrue();
        }

        [Test]
        public void Acquire_ReturnsLockHandle_ForExclusiveLock()
        {
            using var handle = FileLock.Acquire(MakeLockOptions(LockType.Exclusive));

            handle.Should().NotBeNull();
            lockFilePath.Exists.Should().BeTrue();
        }

        [Test]
        public void Acquire_AllowsConcurrentSharedLocks()
        {
            using var firstHandle = FileLock.Acquire(MakeLockOptions(LockType.Shared));

            // A second shared lock on the same file should also succeed
            Action acquireSecond = () =>
            {
                using var secondHandle = FileLock.Acquire(MakeLockOptions(LockType.Shared));
            };

            acquireSecond.Should().NotThrow();
        }

        [TestCase(LockType.Exclusive)]
        [TestCase(LockType.Shared)]
        public void Acquire_ThrowsLockRejectedException_WhenExclusiveLockHeld(LockType secondLockType)
        {
            using var exclusiveHandle = FileLock.Acquire(MakeLockOptions(LockType.Exclusive));

            // Any second acquisition (shared or exclusive) should fail while the exclusive lock is held
            Action acquireSecond = () =>
            {
                using var secondHandle = FileLock.Acquire(MakeLockOptions(secondLockType));
            };

            acquireSecond.Should().Throw<LockRejectedException>();
        }

        [Test]
        public void Acquire_ThrowsLockRejectedException_WhenSharedLockBlocksExclusive()
        {
            using var sharedHandle = FileLock.Acquire(MakeLockOptions(LockType.Shared));

            Action acquireExclusive = () =>
            {
                using var exclusiveHandle = FileLock.Acquire(MakeLockOptions(LockType.Exclusive));
            };

            acquireExclusive.Should().Throw<LockRejectedException>();
        }

        [Test]
        public void Dispose_ReleasesLock_AllowingSubsequentAcquisition()
        {
            var handle = FileLock.Acquire(MakeLockOptions(LockType.Exclusive));
            handle.Dispose();

            // After releasing, a new exclusive lock should succeed
            Action acquireAfterDispose = () =>
            {
                using var secondHandle = FileLock.Acquire(MakeLockOptions(LockType.Exclusive));
            };

            acquireAfterDispose.Should().NotThrow();
        }

        [Test]
        public async Task DisposeAsync_ReleasesLock_AllowingSubsequentAcquisition()
        {
            var handle = FileLock.Acquire(MakeLockOptions(LockType.Exclusive));
            await handle.DisposeAsync();

            Action acquireAfterDispose = () =>
            {
                using var secondHandle = FileLock.Acquire(MakeLockOptions(LockType.Exclusive));
            };

            acquireAfterDispose.Should().NotThrow();
        }
        [Test]
        public void Throw_IncludesLockFilePathAndCause_WhenFailureIsNotContention()
        {
            var missingDirectory = new LockDirectory(new DirectoryInfo(Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}")),
                                                     LockCapability.Supported);
            var lockFile = missingDirectory.GetLockFile("ScriptIsolation.RunningScript.lock");
            var cause = new DirectoryNotFoundException($"Could not find a part of the path '{lockFile.File.FullName}'.");

            var act = () => LockRejectedException.Throw(cause, lockFile);

            act.Should().Throw<LockRejectedException>()
               .Where(e => e.Message.StartsWith("Lock acquisition failed")
                           && e.Message.Contains(lockFile.File.FullName)
                           && e.Message.Contains(cause.Message))
               .And.InnerException.Should().BeSameAs(cause);
        }

        [Test]
        public void Throw_IncludesCause_WhenNoLockFileIsProvided()
        {
            var cause = new UnauthorizedAccessException("Access to the path '/tmp/octopus.target' is denied.");

            var act = () => LockRejectedException.Throw(cause);

            act.Should().Throw<LockRejectedException>()
               .WithMessage($"Lock acquisition failed: {cause.Message}");
        }

        [Test]
        public void Throw_RethrowsLockRejectedExceptionUnchanged()
        {
            var original = new LockRejectedException("exclusive lock is already held");

            var act = () => LockRejectedException.Throw(original, lockFilePath);

            act.Should().Throw<LockRejectedException>().Which.Should().BeSameAs(original);
        }
    }
}
