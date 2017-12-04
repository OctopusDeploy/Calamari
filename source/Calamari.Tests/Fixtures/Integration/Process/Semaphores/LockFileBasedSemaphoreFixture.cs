using System;
using System.Diagnostics;
using System.Threading;
using Calamari.Integration.Processes.Semaphores;
using Calamari.Tests.Helpers;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Integration.Process.Semaphores
{
    [TestFixture]
    public class LockFileBasedSemaphoreFixture
    {
        [Test]
        public void AttemptsToAquireLockIfLockFileDoesNotExist()
        {
            var lockIo = Substitute.For<ILockIo>();
            var name = Guid.NewGuid().ToString();
            var semaphore = new LockFileBasedSemaphore(name, TimeSpan.FromSeconds(30), lockIo, Substitute.For<IProcessFinder>());
            var fileLock = new FileLock();
            lockIo.LockExists(Arg.Any<string>()).Returns(false);
            var result = semaphore.ShouldAquireLock(fileLock);
            Assert.That(result, Is.EqualTo(LockFileBasedSemaphore.AquireLockAction.AquireLock));
        }

        [Test]
        public void DoesNotAttemptToAquireLockIfLockFileIsLockedByOtherProcess()
        {
            var lockIo = Substitute.For<ILockIo>();
            var name = Guid.NewGuid().ToString();
            var semaphore = new LockFileBasedSemaphore(name, TimeSpan.FromSeconds(30), lockIo, Substitute.For<IProcessFinder>());
            var fileLock = new OtherProcessHasExclusiveLockOnFileLock();
            lockIo.LockExists(Arg.Any<string>()).Returns(true);
            var result = semaphore.ShouldAquireLock(fileLock);
            Assert.That(result, Is.EqualTo(LockFileBasedSemaphore.AquireLockAction.DontAquireLock));
        }

        [Test]
        public void DoesNotAttemptToAquireLockIfWeCantDeserialise()
        {
            var lockIo = Substitute.For<ILockIo>();
            var name = Guid.NewGuid().ToString();
            var semaphore = new LockFileBasedSemaphore(name, TimeSpan.FromSeconds(30), lockIo, Substitute.For<IProcessFinder>());
            var fileLock = new UnableToDeserialiseLockFile(DateTime.Now);
            lockIo.LockExists(Arg.Any<string>()).Returns(true);
            var result = semaphore.ShouldAquireLock(fileLock);
            Assert.That(result, Is.EqualTo(LockFileBasedSemaphore.AquireLockAction.DontAquireLock));
        }

        [Test]
        public void AttemptsToAquireLockIfWeCantDeserialiseButFileIsOlderThanLockTimeout()
        {
            var log = new InMemoryLog();
            var lockIo = Substitute.For<ILockIo>();
            var name = Guid.NewGuid().ToString();
            var semaphore = new LockFileBasedSemaphore(name, TimeSpan.FromSeconds(30), lockIo, Substitute.For<IProcessFinder>(), log);
            var fileLock = new UnableToDeserialiseLockFile(DateTime.Now.Subtract(TimeSpan.FromMinutes(5)));
            lockIo.LockExists(Arg.Any<string>()).Returns(true);
            var result = semaphore.ShouldAquireLock(fileLock);
            Assert.That(result, Is.EqualTo(LockFileBasedSemaphore.AquireLockAction.AquireLock));
            log.Messages.Should().Contain(m => m.Level == InMemoryLog.Level.Warn && m.FormattedMessage == "Lock file existed but was not readable, and has existed for longer than lock timeout. Taking lock.");
        }

        [Test]
        public void AttemptsToAquireLockIfWeCantFindLockFile()
        {
            var lockIo = Substitute.For<ILockIo>();
            var name = Guid.NewGuid().ToString();
            var semaphore = new LockFileBasedSemaphore(name, TimeSpan.FromSeconds(30), lockIo, Substitute.For<IProcessFinder>());
            var fileLock = new MissingFileLock();
            //when we check for the lock file, it exists
            lockIo.LockExists(Arg.Any<string>()).Returns(true);
            //but when we read it, its been released
            var result = semaphore.ShouldAquireLock(fileLock);
            Assert.That(result, Is.EqualTo(LockFileBasedSemaphore.AquireLockAction.AquireLock));
        }

        [Test]
        public void AttemptsToAquireLockIfItBelongsToUs()
        {
            var lockIo = Substitute.For<ILockIo>();
            var name = Guid.NewGuid().ToString();
            var semaphore = new LockFileBasedSemaphore(name, TimeSpan.FromSeconds(30), lockIo, Substitute.For<IProcessFinder>());
            var fileLock = new FileLock
            {
                ProcessId = System.Diagnostics.Process.GetCurrentProcess().Id,
                ThreadId = Thread.CurrentThread.ManagedThreadId
            };
            lockIo.LockExists(Arg.Any<string>()).Returns(true);
            var result = semaphore.ShouldAquireLock(fileLock);
            Assert.That(result, Is.EqualTo(LockFileBasedSemaphore.AquireLockAction.AquireLock));
        }

        [Test]
        public void AttemptsToAquireLockIfOtherProcessIsNoLongerRunning()
        {
            var log = new InMemoryLog();
            var lockIo = Substitute.For<ILockIo>();
            var name = Guid.NewGuid().ToString();
            var processFinder = Substitute.For<IProcessFinder>();

            processFinder.ProcessIsRunning(Arg.Any<int>(), Arg.Any<string>()).Returns(false);
            var semaphore = new LockFileBasedSemaphore(name, TimeSpan.FromSeconds(30), lockIo, processFinder, log);
            var fileLock = new FileLock
            {
                ProcessId = -1,
                ThreadId = -2,
                ProcessName = Guid.NewGuid().ToString()
            };
            lockIo.LockExists(Arg.Any<string>()).Returns(true);
            var result = semaphore.ShouldAquireLock(fileLock);
            Assert.That(result, Is.EqualTo(LockFileBasedSemaphore.AquireLockAction.AquireLock));
            log.Messages.Should().Contain(m => m.Level == InMemoryLog.Level.Warn && m.FormattedMessage == "Process -1, thread -2 had lock, but appears to have crashed. Taking lock.");
        }

        [Test]
        public void DoesNotAttemptToAquireLockIfOwnedBySomeoneElseAndLockHasNotTimedOut()
        {
            var lockIo = Substitute.For<ILockIo>();
            var name = Guid.NewGuid().ToString();
            var processFinder = Substitute.For<IProcessFinder>();
            processFinder.ProcessIsRunning(Arg.Any<int>(), Arg.Any<string>()).Returns(true);
            var semaphore = new LockFileBasedSemaphore(name, TimeSpan.FromSeconds(30), lockIo, processFinder);
            var fileLock = new FileLock { Timestamp = DateTime.Now.Ticks };
            lockIo.LockExists(Arg.Any<string>()).Returns(true);
            var result = semaphore.ShouldAquireLock(fileLock);
            Assert.That(result, Is.EqualTo(LockFileBasedSemaphore.AquireLockAction.DontAquireLock));
        }

        [Test]
        public void AttemptsToForciblyAquireLockIfOwnedBySomeoneElseAndLockHasTimedOut()
        {
            var log = new InMemoryLog();
            var lockIo = Substitute.For<ILockIo>();
            var name = Guid.NewGuid().ToString();
            var processFinder = Substitute.For<IProcessFinder>();
            processFinder.ProcessIsRunning(Arg.Any<int>(), Arg.Any<string>()).Returns(true);
            var semaphore = new LockFileBasedSemaphore(name, TimeSpan.FromSeconds(30), lockIo, processFinder, log);
            var fileLock = new FileLock { Timestamp = (DateTime.Now.Subtract(TimeSpan.FromMinutes(5))).Ticks };
            lockIo.LockExists(Arg.Any<string>()).Returns(true);
            var result = semaphore.ShouldAquireLock(fileLock);
            Assert.That(result, Is.EqualTo(LockFileBasedSemaphore.AquireLockAction.ForciblyAquireLock));
            log.Messages.Should().Contain(m => m.Level == InMemoryLog.Level.Warn && m.FormattedMessage == "Forcibly taking lock from process 0, thread 0 as lock has timed out. If this happens regularly, please contact Octopus Support.");
        }

        [Test]
        public void DeletesLockIfOwnedBySomeoneElseAndLockHasTimedOut()
        {
            var lockIo = Substitute.For<ILockIo>();
            var name = Guid.NewGuid().ToString();
            var processFinder = Substitute.For<IProcessFinder>();
            processFinder.ProcessIsRunning(Arg.Any<int>(), Arg.Any<string>()).Returns(true);
            var semaphore = new LockFileBasedSemaphore(name, TimeSpan.FromSeconds(30), lockIo, processFinder);
            //not setting processid/threadid, therefore its someone elses
            var fileLock = new FileLock { Timestamp = (DateTime.Now.Subtract(TimeSpan.FromMinutes(5))).Ticks };
            lockIo.ReadLock(Arg.Any<string>()).Returns(fileLock);
            lockIo.LockExists(Arg.Any<string>()).Returns(true);
            lockIo.WriteLock(Arg.Any<string>(), Arg.Any<FileLock>()).Returns(true);
            var result = semaphore.TryAcquireLock();
            Assert.That(result, Is.True);
            lockIo.Received().DeleteLock(Arg.Any<string>());
            lockIo.Received().WriteLock(Arg.Any<string>(), Arg.Any<FileLock>());
        }

        [Test]
        public void WritesLockFile()
        {
            var lockIo = Substitute.For<ILockIo>();
            var name = Guid.NewGuid().ToString();
            var processFinder = Substitute.For<IProcessFinder>();
            processFinder.ProcessIsRunning(Arg.Any<int>(), Arg.Any<string>()).Returns(true);
            var semaphore = new LockFileBasedSemaphore(name, TimeSpan.FromSeconds(30), lockIo, processFinder);
            var fileLock = new FileLock
            {
                ProcessId = System.Diagnostics.Process.GetCurrentProcess().Id,
                ThreadId = Thread.CurrentThread.ManagedThreadId
            };
            lockIo.ReadLock(Arg.Any<string>()).Returns(fileLock);
            lockIo.LockExists(Arg.Any<string>()).Returns(true);
            lockIo.WriteLock(Arg.Any<string>(), Arg.Any<FileLock>()).Returns(true);
            var result = semaphore.TryAcquireLock();
            Assert.That(result, Is.True);
            lockIo.DidNotReceive().DeleteLock(Arg.Any<string>());
            lockIo.Received().WriteLock(Arg.Any<string>(), Arg.Any<FileLock>());
        }

        [Test]
        public void CanReleaseLockIfWeCanAquireIt()
        {
            var lockIo = Substitute.For<ILockIo>();
            var name = Guid.NewGuid().ToString();
            var semaphore = new LockFileBasedSemaphore(name, TimeSpan.FromSeconds(30), lockIo, Substitute.For<IProcessFinder>());
            var fileLock = new FileLock
            {
                ProcessId = System.Diagnostics.Process.GetCurrentProcess().Id,
                ThreadId = Thread.CurrentThread.ManagedThreadId
            };
            lockIo.ReadLock(Arg.Any<string>()).Returns(fileLock);
            lockIo.LockExists(Arg.Any<string>()).Returns(true);
            lockIo.WriteLock(Arg.Any<string>(), Arg.Any<FileLock>()).Returns(true);
            semaphore.ReleaseLock();
            lockIo.Received().DeleteLock(Arg.Any<string>());
        }

        [Test]
        public void CannotReleaseLockIfWeCannotAquireIt()
        {
            var lockIo = Substitute.For<ILockIo>();
            var name = Guid.NewGuid().ToString();
            var semaphore = new LockFileBasedSemaphore(name, TimeSpan.FromSeconds(30), lockIo, Substitute.For<IProcessFinder>());
            var fileLock = new FileLock
            {
                ProcessId = System.Diagnostics.Process.GetCurrentProcess().Id,
                ThreadId = Thread.CurrentThread.ManagedThreadId
            };
            lockIo.ReadLock(Arg.Any<string>()).Returns(fileLock);
            lockIo.LockExists(Arg.Any<string>()).Returns(true);
            //failed to write the lock file (ie, someone else got in first)
            lockIo.WriteLock(Arg.Any<string>(), Arg.Any<FileLock>()).Returns(false);
            semaphore.ReleaseLock();
            lockIo.DidNotReceive().DeleteLock(Arg.Any<string>());
        }

        [Test]
        public void WaitOneWithTimeoutTimesOut()
        {
            var lockIo = Substitute.For<ILockIo>();
            var name = Guid.NewGuid().ToString();
            var semaphore = new LockFileBasedSemaphore(name, TimeSpan.FromSeconds(30), lockIo, Substitute.For<IProcessFinder>());
            var fileLock = new FileLock
            {
                ProcessId = System.Diagnostics.Process.GetCurrentProcess().Id,
                ThreadId = Thread.CurrentThread.ManagedThreadId
            };
            lockIo.ReadLock(Arg.Any<string>()).Returns(fileLock);
            lockIo.LockExists(Arg.Any<string>()).Returns(true);
            //failed to write the lock file (ie, someone else got in first)
            lockIo.WriteLock(Arg.Any<string>(), Arg.Any<FileLock>()).Returns(false);

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var result = semaphore.WaitOne(300);
            Assert.That(result, Is.False);
            Assert.That(stopwatch.ElapsedMilliseconds, Is.GreaterThan(300));
        }

        [Test]
        public void WaitOneWithTimeoutDoesNotWaitIfCanAquireLock()
        {
            var lockIo = Substitute.For<ILockIo>();
            var name = Guid.NewGuid().ToString();
            var semaphore = new LockFileBasedSemaphore(name, TimeSpan.FromSeconds(30), lockIo, Substitute.For<IProcessFinder>());
            var fileLock = new FileLock
            {
                ProcessId = System.Diagnostics.Process.GetCurrentProcess().Id,
                ThreadId = Thread.CurrentThread.ManagedThreadId
            };
            lockIo.ReadLock(Arg.Any<string>()).Returns(fileLock);
            lockIo.LockExists(Arg.Any<string>()).Returns(true);
            lockIo.WriteLock(Arg.Any<string>(), Arg.Any<FileLock>()).Returns(true);

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var result = semaphore.WaitOne(300);
            Assert.That(result, Is.True);
            Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(100));
        }

        [Test]
        public void WaitOneWithTimeoutReturnsAfterAquiringLock()
        {
            var lockIo = Substitute.For<ILockIo>();
            var name = Guid.NewGuid().ToString();
            var semaphore = new LockFileBasedSemaphore(name, TimeSpan.FromSeconds(30), lockIo, Substitute.For<IProcessFinder>());
            var fileLock = new FileLock
            {
                ProcessId = System.Diagnostics.Process.GetCurrentProcess().Id,
                ThreadId = Thread.CurrentThread.ManagedThreadId
            };
            lockIo.ReadLock(Arg.Any<string>()).Returns(fileLock);
            lockIo.LockExists(Arg.Any<string>()).Returns(true);
            lockIo.WriteLock(Arg.Any<string>(), Arg.Any<FileLock>()).Returns(false, false, false, true);

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var result = semaphore.WaitOne(500);
            Assert.That(result, Is.True);
            Assert.That(stopwatch.ElapsedMilliseconds, Is.GreaterThan(200));
        }

        [Test]
        public void WaitOneReturnsAfterAquiringLock()
        {
            var lockIo = Substitute.For<ILockIo>();
            var name = Guid.NewGuid().ToString();
            var semaphore = new LockFileBasedSemaphore(name, TimeSpan.FromSeconds(30), lockIo, Substitute.For<IProcessFinder>());
            var fileLock = new FileLock
            {
                ProcessId = System.Diagnostics.Process.GetCurrentProcess().Id,
                ThreadId = Thread.CurrentThread.ManagedThreadId
            };
            lockIo.ReadLock(Arg.Any<string>()).Returns(fileLock);
            lockIo.LockExists(Arg.Any<string>()).Returns(true);
            lockIo.WriteLock(Arg.Any<string>(), Arg.Any<FileLock>()).Returns(false, false, true);

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var result = semaphore.WaitOne();
            Assert.That(result, Is.True);
            Assert.That(stopwatch.ElapsedMilliseconds, Is.GreaterThan(175));
        }
    }
}