using System;
using Calamari.Common.Features.Processes.Semaphores;
using Calamari.Common.Plumbing.Logging;
using Calamari.Testing.Helpers;
using Calamari.Tests.Helpers;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Integration.Process.Semaphores
{
    [TestFixture]
    public class FileBasedSempahoreManagerFixture : SemaphoreFixtureBase
    {
        [Test]
        public void FileBasedSempahoreWaitsUntilFirstSemaphoreIsReleased()
        {
            SecondSemaphoreWaitsUntilFirstSemaphoreIsReleased(new FileBasedSempahoreManager());
        }

        [Test]
        public void FileBasedSempahoreShouldIsolate()
        {
            ShouldIsolate(new FileBasedSempahoreManager());
        }

        [Test]
        public void WritesMessageToLogIfCannotGetSemaphore()
        {
            var log = new InMemoryLog();
            var semaphoreCreator = Substitute.For<ICreateSemaphores>();
            var name = Guid.NewGuid().ToString();
            var semaphore = Substitute.For<ISemaphore>();
            semaphore.WaitOne(200).Returns(false);
            semaphoreCreator.Create(name, TimeSpan.FromSeconds(60)).Returns(semaphore);
            var sempahoreManager = new FileBasedSempahoreManager(log, TimeSpan.FromMilliseconds(200), semaphoreCreator);
            using (sempahoreManager.Acquire(name, "wait message")) { }

            log.Messages.Should().Contain(m => m.Level == InMemoryLog.Level.Verbose && m.FormattedMessage == "wait message");
        }

        [Test]
        public void ReleasesLockOnDispose()
        {
            var semaphoreCreator = Substitute.For<ICreateSemaphores>();
            var name = Guid.NewGuid().ToString();
            var semaphore = Substitute.For<ISemaphore>();
            semaphoreCreator.Create(name, TimeSpan.FromMinutes(2)).Returns(semaphore);
            var sempahoreManager = new FileBasedSempahoreManager(Substitute.For<ILog>(), TimeSpan.FromMilliseconds(200), semaphoreCreator);
            using (sempahoreManager.Acquire(name, "wait message")) { }
            semaphore.Received().ReleaseLock();
        }

        [Test]
        public void AttemptsToGetLockWithTimeoutThenWaitsIndefinitely()
        {
            var semaphoreCreator = Substitute.For<ICreateSemaphores>();
            var name = Guid.NewGuid().ToString();
            var semaphore = Substitute.For<ISemaphore>();
            semaphore.WaitOne(200).Returns(false);
            semaphoreCreator.Create(name, TimeSpan.FromMinutes(2)).Returns(semaphore);
            var sempahoreManager = new FileBasedSempahoreManager(Substitute.For<ILog>(), TimeSpan.FromMilliseconds(200), semaphoreCreator);
            using (sempahoreManager.Acquire(name, "wait message")) { }
            semaphore.Received().WaitOne(200);
            semaphore.Received().WaitOne();
        }
    }
}