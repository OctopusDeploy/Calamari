using System;
using System.Threading;
using Calamari.Common.Features.Processes.Semaphores;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Integration.Process.Semaphores
{
    [TestFixture]
    public class FileLockFixture
    {
        [Test]
        [TestCase(123, 456, "ProcessName", 123, 456, "ProcessName", true)]
        [TestCase(123, 456, "ProcessName", 789, 456, "ProcessName", false)]
        [TestCase(123, 456, "ProcessName", 123, 789, "ProcessName", false)]
        [TestCase(123, 456, "ProcessName", 123, 456, "DiffProcessName", false)]
        [TestCase(123, 456, "ProcessName", 789, 246, "ProcessName", false)]
        [TestCase(123, 456, "ProcessName", 123, 246, "DiffProcessName", false)]
        [TestCase(123, 456, "ProcessName", 789, 456, "ProcessName", false)]
        [TestCase(123, 456, "ProcessName", 789, 246, "DiffProcessName", false)]
        public void EqualsComparesCorrectly(int processIdA, int threadIdA, string processNameA, int processIdB, int threadIdB, string processNameB, bool expectedResult)
        {
            var objectA = new FileLock(processIdA, processNameA, threadIdA, DateTime.Now.Ticks);
            var objectB = new FileLock(processIdB, processNameB, threadIdB, DateTime.Now.Ticks);
            Assert.That(Equals(objectA, objectB), Is.EqualTo(expectedResult));
        }

        [Test]
        public void EqualsIgnoresTimestamp()
        {
            var objectA = new FileLock(123, "ProcessName", 456, DateTime.Now.Ticks);
            var objectB = new FileLock(123, "ProcessName", 456, DateTime.Now.Ticks + 5);
            Assert.That(Equals(objectA, objectB), Is.True);
        }

        [Test]
        public void EqualsReturnsFalseIfOtherObjectIsNull()
        {
            var fileLock = new FileLock(123, "ProcessName", 456, DateTime.Now.Ticks);
            Assert.That(fileLock.Equals(null), Is.False);
        }

        [Test]
        public void EqualsReturnsFalseIfOtherObjectIsDifferentType()
        {
            var fileLock = new FileLock(123, "ProcessName", 456, DateTime.Now.Ticks);
            Assert.That(fileLock.Equals(new object()), Is.False);
        }

        [Test]
        public void EqualsReturnsTrueIfSameObject()
        {
            var fileLock = new FileLock(123, "ProcessName", 456, DateTime.Now.Ticks);
            // ReSharper disable once EqualExpressionComparison
            Assert.That(fileLock.Equals(fileLock), Is.True);
        }

        [Test]
        public void BelongsToCurrentProcessAndThreadMatchesOnCurrentProcessAndThread()
        {
            var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
            var fileLock = new FileLock(
                currentProcess.Id,
                currentProcess.ProcessName,
                Thread.CurrentThread.ManagedThreadId,
                DateTime.Now.Ticks
            );
            Assert.That(fileLock.BelongsToCurrentProcessAndThread(), Is.True);
        }

        [Test]
        public void BelongsToCurrentProcessAndThreadReturnsFalseIfIncorrectProcessId()
        {
            var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
            var fileLockWithIncorrectProcessId = new FileLock(
                -100,
                currentProcess.ProcessName,
                Thread.CurrentThread.ManagedThreadId,
                DateTime.Now.Ticks
            );
            Assert.That(fileLockWithIncorrectProcessId.BelongsToCurrentProcessAndThread(), Is.False);
        }

        [Test]
        public void BelongsToCurrentProcessAndThreadReturnsFalseIfIncorrectThreadId()
        {
            var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
            var fileLockWithIncorrectThreadId = new FileLock(
                currentProcess.Id,
                currentProcess.ProcessName,
                -200,
                DateTime.Now.Ticks
            );
            Assert.That(fileLockWithIncorrectThreadId.BelongsToCurrentProcessAndThread(), Is.False);
        }

    }
}
