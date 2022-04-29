using System;
using System.Diagnostics;
using System.Threading;
using Calamari.Common.Features.Processes.Semaphores;
using Calamari.Testing.Helpers;
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
        public void AttemptsToAcquireLockIfLockFileDoesNotExist()
        {
            var lockIo = Substitute.For<ILockIo>();
            var name = Guid.NewGuid().ToString();
            var semaphore = new LockFileBasedSemaphore(name, TimeSpan.FromSeconds(30), lockIo, Substitute.For<IProcessFinder>(), new InMemoryLog());
            var fileLock = new FileLock(System.Diagnostics.Process.GetCurrentProcess().Id, Guid.NewGuid().ToString(), Thread.CurrentThread.ManagedThreadId, DateTime.Now.Ticks);
            lockIo.LockExists(Arg.Any<string>()).Returns(false);
            var result = semaphore.ShouldAcquireLock(fileLock);
            Assert.That(result, Is.EqualTo(LockFileBasedSemaphore.AcquireLockAction.AcquireLock));
        }

        [Test]
        public void DoesNotAttemptToAcquireLockIfLockFileIsLockedByOtherProcess()
        {
            var lockIo = Substitute.For<ILockIo>();
            var name = Guid.NewGuid().ToString();
            var semaphore = new LockFileBasedSemaphore(name, TimeSpan.FromSeconds(30), lockIo, Substitute.For<IProcessFinder>(), new InMemoryLog());
            var fileLock = new OtherProcessHasExclusiveLockOnFileLock();
            lockIo.LockExists(Arg.Any<string>()).Returns(true);
            var result = semaphore.ShouldAcquireLock(fileLock);
            Assert.That(result, Is.EqualTo(LockFileBasedSemaphore.AcquireLockAction.DontAcquireLock));
        }

        [Test]
        public void DoesNotAttemptToAcquireLockIfWeCantDeserialise()
        {
            var lockIo = Substitute.For<ILockIo>();
            var name = Guid.NewGuid().ToString();
            var semaphore = new LockFileBasedSemaphore(name, TimeSpan.FromSeconds(30), lockIo, Substitute.For<IProcessFinder>(), new InMemoryLog());
            var fileLock = new UnableToDeserialiseLockFile(DateTime.Now);
            lockIo.LockExists(Arg.Any<string>()).Returns(true);
            var result = semaphore.ShouldAcquireLock(fileLock);
            Assert.That(result, Is.EqualTo(LockFileBasedSemaphore.AcquireLockAction.DontAcquireLock));
        }

        [Test]
        public void AttemptsToAcquireLockIfWeCantDeserialiseButFileIsOlderThanLockTimeout()
        {
            var log = new InMemoryLog();
            var lockIo = Substitute.For<ILockIo>();
            var name = Guid.NewGuid().ToString();
            var semaphore = new LockFileBasedSemaphore(name, TimeSpan.FromSeconds(30), lockIo, Substitute.For<IProcessFinder>(), log);
            var fileLock = new UnableToDeserialiseLockFile(DateTime.Now.Subtract(TimeSpan.FromMinutes(5)));
            lockIo.LockExists(Arg.Any<string>()).Returns(true);
            var result = semaphore.ShouldAcquireLock(fileLock);
            Assert.That(result, Is.EqualTo(LockFileBasedSemaphore.AcquireLockAction.AcquireLock));
            log.Messages.Should().Contain(m => m.Level == InMemoryLog.Level.Warn && m.FormattedMessage == "Lock file existed but was not readable, and has existed for longer than lock timeout. Taking lock.");
        }

        [Test]
        public void AttemptsToAcquireLockIfWeCantFindLockFile()
        {
            var lockIo = Substitute.For<ILockIo>();
            var name = Guid.NewGuid().ToString();
            var semaphore = new LockFileBasedSemaphore(name, TimeSpan.FromSeconds(30), lockIo, Substitute.For<IProcessFinder>(), new InMemoryLog());
            var fileLock = new MissingFileLock();
            //when we check for the lock file, it exists
            lockIo.LockExists(Arg.Any<string>()).Returns(true);
            //but when we read it, its been released
            var result = semaphore.ShouldAcquireLock(fileLock);
            Assert.That(result, Is.EqualTo(LockFileBasedSemaphore.AcquireLockAction.AcquireLock));
        }

        [Test]
        public void AttemptsToAcquireLockIfItBelongsToUs()
        {
            var lockIo = Substitute.For<ILockIo>();
            var name = Guid.NewGuid().ToString();
            var semaphore = new LockFileBasedSemaphore(name, TimeSpan.FromSeconds(30), lockIo, Substitute.For<IProcessFinder>(), new InMemoryLog());
            var fileLock = new FileLock(System.Diagnostics.Process.GetCurrentProcess().Id, Guid.NewGuid().ToString(), Thread.CurrentThread.ManagedThreadId, DateTime.Now.Ticks);
            lockIo.LockExists(Arg.Any<string>()).Returns(true);
            var result = semaphore.ShouldAcquireLock(fileLock);
            Assert.That(result, Is.EqualTo(LockFileBasedSemaphore.AcquireLockAction.AcquireLock));
        }

        [Test]
        public void AttemptsToAcquireLockIfOtherProcessIsNoLongerRunning()
        {
            var log = new InMemoryLog();
            var lockIo = Substitute.For<ILockIo>();
            var name = Guid.NewGuid().ToString();
            var processFinder = Substitute.For<IProcessFinder>();

            processFinder.ProcessIsRunning(Arg.Any<int>(), Arg.Any<string>()).Returns(false);
            var semaphore = new LockFileBasedSemaphore(name, TimeSpan.FromSeconds(30), lockIo, processFinder, log);
            var fileLock = new FileLock(-1, Guid.NewGuid().ToString(), -2, DateTime.Now.Ticks);
            lockIo.LockExists(Arg.Any<string>()).Returns(true);
            var result = semaphore.ShouldAcquireLock(fileLock);
            Assert.That(result, Is.EqualTo(LockFileBasedSemaphore.AcquireLockAction.AcquireLock));
            log.Messages.Should().Contain(m => m.Level == InMemoryLog.Level.Warn && m.FormattedMessage == "Process -1, thread -2 had lock, but appears to have crashed. Taking lock.");
        }

        [Test]
        public void DoesNotAttemptToAcquireLockIfOwnedBySomeoneElseAndLockHasNotTimedOut()
        {
            var lockIo = Substitute.For<ILockIo>();
            var name = Guid.NewGuid().ToString();
            var processFinder = Substitute.For<IProcessFinder>();
            processFinder.ProcessIsRunning(Arg.Any<int>(), Arg.Any<string>()).Returns(true);
            var semaphore = new LockFileBasedSemaphore(name, TimeSpan.FromSeconds(30), lockIo, processFinder, new InMemoryLog());
            var fileLock = new FileLock(0, Guid.NewGuid().ToString(), 0, DateTime.Now.Ticks);
            lockIo.LockExists(Arg.Any<string>()).Returns(true);
            var result = semaphore.ShouldAcquireLock(fileLock);
            Assert.That(result, Is.EqualTo(LockFileBasedSemaphore.AcquireLockAction.DontAcquireLock));
        }

        [Test]
        public void AttemptsToForciblyAcquireLockIfOwnedBySomeoneElseAndLockHasTimedOut()
        {
            var log = new InMemoryLog();
            var lockIo = Substitute.For<ILockIo>();
            var name = Guid.NewGuid().ToString();
            var processFinder = Substitute.For<IProcessFinder>();
            processFinder.ProcessIsRunning(Arg.Any<int>(), Arg.Any<string>()).Returns(true);
            var semaphore = new LockFileBasedSemaphore(name, TimeSpan.FromSeconds(30), lockIo, processFinder, log);
            var fileLock = new FileLock(0, Guid.NewGuid().ToString(), 0, DateTime.Now.Subtract(TimeSpan.FromMinutes(5)).Ticks);
            lockIo.LockExists(Arg.Any<string>()).Returns(true);
            var result = semaphore.ShouldAcquireLock(fileLock);
            Assert.That(result, Is.EqualTo(LockFileBasedSemaphore.AcquireLockAction.ForciblyAcquireLock));
            log.Messages.Should().Contain(m => m.Level == InMemoryLog.Level.Warn && m.FormattedMessage == "Forcibly taking lock from process 0, thread 0 as lock has timed out. If this happens regularly, please contact Octopus Support.");
        }

        [Test]
        public void DeletesLockIfOwnedBySomeoneElseAndLockHasTimedOut()
        {
            var lockIo = Substitute.For<ILockIo>();
            var name = Guid.NewGuid().ToString();
            var processFinder = Substitute.For<IProcessFinder>();
            processFinder.ProcessIsRunning(Arg.Any<int>(), Arg.Any<string>()).Returns(true);
            var semaphore = new LockFileBasedSemaphore(name, TimeSpan.FromSeconds(30), lockIo, processFinder, new InMemoryLog());
            //not setting processid/threadid, therefore its someone elses
            var fileLock = new FileLock(0, Guid.NewGuid().ToString(), 0, DateTime.Now.Subtract(TimeSpan.FromMinutes(5)).Ticks);
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
            var semaphore = new LockFileBasedSemaphore(name, TimeSpan.FromSeconds(30), lockIo, processFinder, new InMemoryLog());
            var fileLock = new FileLock(System.Diagnostics.Process.GetCurrentProcess().Id, Guid.NewGuid().ToString(), Thread.CurrentThread.ManagedThreadId, DateTime.Now.Ticks);
            lockIo.ReadLock(Arg.Any<string>()).Returns(fileLock);
            lockIo.LockExists(Arg.Any<string>()).Returns(true);
            lockIo.WriteLock(Arg.Any<string>(), Arg.Any<FileLock>()).Returns(true);
            var result = semaphore.TryAcquireLock();
            Assert.That(result, Is.True);
            lockIo.DidNotReceive().DeleteLock(Arg.Any<string>());
            lockIo.Received().WriteLock(Arg.Any<string>(), Arg.Any<FileLock>());
        }

        [Test]
        public void CanReleaseLockIfWeCanAcquireIt()
        {
            var lockIo = Substitute.For<ILockIo>();
            var name = Guid.NewGuid().ToString();
            var semaphore = new LockFileBasedSemaphore(name, TimeSpan.FromSeconds(30), lockIo, Substitute.For<IProcessFinder>(), new InMemoryLog());
            var fileLock = new FileLock(System.Diagnostics.Process.GetCurrentProcess().Id, Guid.NewGuid().ToString(), Thread.CurrentThread.ManagedThreadId, DateTime.Now.Ticks);
            lockIo.ReadLock(Arg.Any<string>()).Returns(fileLock);
            lockIo.LockExists(Arg.Any<string>()).Returns(true);
            lockIo.WriteLock(Arg.Any<string>(), Arg.Any<FileLock>()).Returns(true);
            semaphore.ReleaseLock();
            lockIo.Received().DeleteLock(Arg.Any<string>());
        }

        [Test]
        public void CannotReleaseLockIfWeCannotAcquireIt()
        {
            var lockIo = Substitute.For<ILockIo>();
            var name = Guid.NewGuid().ToString();
            var semaphore = new LockFileBasedSemaphore(name, TimeSpan.FromSeconds(30), lockIo, Substitute.For<IProcessFinder>(), new InMemoryLog());
            var fileLock = new FileLock(System.Diagnostics.Process.GetCurrentProcess().Id, Guid.NewGuid().ToString(), Thread.CurrentThread.ManagedThreadId, DateTime.Now.Ticks);
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
            var semaphore = new LockFileBasedSemaphore(name, TimeSpan.FromSeconds(30), lockIo, Substitute.For<IProcessFinder>(), new InMemoryLog());
            var fileLock = new FileLock(System.Diagnostics.Process.GetCurrentProcess().Id, Guid.NewGuid().ToString(), Thread.CurrentThread.ManagedThreadId, DateTime.Now.Ticks);
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
        public void WaitOneWithTimeoutDoesNotWaitIfCanAcquireLock()
        {
            var lockIo = Substitute.For<ILockIo>();
            var name = Guid.NewGuid().ToString();
            var semaphore = new LockFileBasedSemaphore(name, TimeSpan.FromSeconds(30), lockIo, Substitute.For<IProcessFinder>(), new InMemoryLog());
            var fileLock = new FileLock(System.Diagnostics.Process.GetCurrentProcess().Id, Guid.NewGuid().ToString(), Thread.CurrentThread.ManagedThreadId, DateTime.Now.Ticks);
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
            var semaphore = new LockFileBasedSemaphore(name, TimeSpan.FromSeconds(30), lockIo, Substitute.For<IProcessFinder>(), new InMemoryLog());
            var fileLock = new FileLock(System.Diagnostics.Process.GetCurrentProcess().Id, Guid.NewGuid().ToString(), Thread.CurrentThread.ManagedThreadId, DateTime.Now.Ticks);
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
            var semaphore = new LockFileBasedSemaphore(name, TimeSpan.FromSeconds(30), lockIo, Substitute.For<IProcessFinder>(), new InMemoryLog());
            var fileLock = new FileLock(System.Diagnostics.Process.GetCurrentProcess().Id, Guid.NewGuid().ToString(), Thread.CurrentThread.ManagedThreadId, DateTime.Now.Ticks);
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