using System;
using System.IO;
using System.Threading.Tasks;
using Calamari.Common.Features.Processes.ScriptIsolation;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.ScriptIsolation
{
    [TestFixture]
    public abstract class FileLockFixture
    {
        FileInfo lockFilePath = null!;

        [SetUp]
        public void SetUp()
        {
            lockFilePath = new(Path.Combine(Path.GetTempPath(), $"ScriptIsolation.FileLockFixture.{Guid.NewGuid()}.lock"));
        }

        [TearDown]
        public void TearDown()
        {
            if (lockFilePath.Exists)
            {
                lockFilePath.Delete();
            }
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
    }
}
