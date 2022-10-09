using System;
using System.IO;
using System.Text;
using System.Threading;
using Calamari.Common.Features.Processes.Semaphores;
using Calamari.Common.Plumbing.FileSystem;
using Newtonsoft.Json;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Integration.Process.Semaphores
{
    [TestFixture]
    public class LockIoTests
    {
        [Test]
        public void ReadLockReturnsMissingFileLockIfFileNotFound()
        {
            var fileSystem = Substitute.For<ICalamariFileSystem>();
            var lockFilePath = "fake path";
            fileSystem.OpenFileExclusively(lockFilePath, FileMode.Open, FileAccess.Read)
                      .Returns(x => { throw new FileNotFoundException(); } );
            var lockIo = new LockIo(fileSystem);
            var result = lockIo.ReadLock(lockFilePath);
            Assert.That(result, Is.InstanceOf<MissingFileLock>());
        }

        [Test]
        public void ReadLockReturnsOtherProcessHasExclusiveLockIfIoException()
        {
            var fileSystem = Substitute.For<ICalamariFileSystem>();
            var lockFilePath = "fake path";
            fileSystem.OpenFileExclusively(lockFilePath, FileMode.Open, FileAccess.Read)
                      .Returns(x => { throw new IOException("Sharing violation"); });
            var lockIo = new LockIo(fileSystem);
            var result = lockIo.ReadLock(lockFilePath);
            Assert.That(result, Is.InstanceOf<OtherProcessHasExclusiveLockOnFileLock>());
        }

        [Test]
        public void ReadLockReturnsUnableToDeserialiseWhenDeserialisationFails()
        {
            var fileSystem = Substitute.For<ICalamariFileSystem>();
            var lockFilePath = "fake path";
            var fileCreationTime = DateTime.Now;
            fileSystem.GetCreationTime(lockFilePath).Returns(fileCreationTime);

            var lockIo = new LockIo(fileSystem);
            fileSystem.OpenFileExclusively(lockFilePath, FileMode.Open, FileAccess.Read)
                      .Returns(x => { throw new JsonReaderException(); });
            var result = lockIo.ReadLock(lockFilePath);
            Assert.That(result, Is.InstanceOf<UnableToDeserialiseLockFile>());
            Assert.That(((UnableToDeserialiseLockFile)result).CreationTime, Is.EqualTo(fileCreationTime));
        }

        [Test]
        public void ReadLockReturnsOtherProcessHasExclusiveLockIfUnknownException()
        {
            var fileSystem = Substitute.For<ICalamariFileSystem>();
            var lockFilePath = "fake path";
            var lockIo = new LockIo(fileSystem);
            fileSystem.OpenFileExclusively(lockFilePath, FileMode.Open, FileAccess.Read)
                      .Returns(x => { throw new ApplicationException(); });
            var result = lockIo.ReadLock(lockFilePath);
            Assert.That(result, Is.InstanceOf<OtherProcessHasExclusiveLockOnFileLock>());
        }

        [Test]
        public void ReadLockReturnsLockDetailsIfLockBelongsToUs()
        {
            var fileSystem = Substitute.For<ICalamariFileSystem>();
            var lockFilePath = "fake path";
            var lockIo = new LockIo(fileSystem);
            var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
            var fileContent = $"{{\"__type\":\"FileLock:#Calamari.Integration.Processes.Semaphores\",\"ProcessId\":{currentProcess.Id},\"ProcessName\":\"{currentProcess.ProcessName}\",\"ThreadId\":{Thread.CurrentThread.ManagedThreadId},\"Timestamp\":636114372739676700}}";
            fileSystem.OpenFileExclusively(lockFilePath, FileMode.Open, FileAccess.Read)
                      .Returns(x => new MemoryStream(Encoding.UTF8.GetBytes(fileContent)));
            var result = lockIo.ReadLock(lockFilePath) as FileLock;
            Assert.That(result, Is.InstanceOf<FileLock>());
            Assert.That(result.ProcessId, Is.EqualTo(currentProcess.Id));
            Assert.That(result.ProcessName, Is.EqualTo(currentProcess.ProcessName));
            Assert.That(result.ThreadId, Is.EqualTo(Thread.CurrentThread.ManagedThreadId));
            Assert.That(result.Timestamp, Is.EqualTo(636114372739676700));
        }

        [Test]
        public void ReadLockReturnsOtherProcessOwnsFileLockIfLockBelongsToSomeoneElse()
        {
            var fileSystem = Substitute.For<ICalamariFileSystem>();
            var lockFilePath = "fake path";
            var lockIo = new LockIo(fileSystem);
            var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
            var fileContent = $"{{\"__type\":\"FileLock:#Calamari.Integration.Processes.Semaphores\",\"ProcessId\":{currentProcess.Id + 1},\"ProcessName\":\"{currentProcess.ProcessName}\",\"ThreadId\":{Thread.CurrentThread.ManagedThreadId},\"Timestamp\":636114372739676700}}";
            fileSystem.OpenFileExclusively(lockFilePath, FileMode.Open, FileAccess.Read)
                      .Returns(x => new MemoryStream(Encoding.UTF8.GetBytes(fileContent)));
            var result = lockIo.ReadLock(lockFilePath) as OtherProcessOwnsFileLock;
            Assert.That(result, Is.InstanceOf<OtherProcessOwnsFileLock>());
            Assert.That(result.ProcessId, Is.EqualTo(currentProcess.Id + 1));
            Assert.That(result.ProcessName, Is.EqualTo(currentProcess.ProcessName));
            Assert.That(result.ThreadId, Is.EqualTo(Thread.CurrentThread.ManagedThreadId));
            Assert.That(result.Timestamp, Is.EqualTo(636114372739676700));
        }

        [Test]
        public void DeleteLockSwallowsExceptions()
        {
            var fileSystem = Substitute.For<ICalamariFileSystem>();
            var lockFilePath = "fake path";
            var lockIo = new LockIo(fileSystem);
            fileSystem.When(x => x.DeleteFile(lockFilePath)).Do(x => { throw new Exception("failed to delete file"); });
            Assert.DoesNotThrow(() => lockIo.DeleteLock(lockFilePath));
        }

        [Test]
        public void WriteLockDoesNotOverwriteLockFileIfItsIdenticalToWhatWeAreWantingToWrite()
        {
            var fileSystem = Substitute.For<ICalamariFileSystem>();
            var lockFilePath = "fake path";
            var lockIo = new LockIo(fileSystem);
            fileSystem.FileExists(lockFilePath).Returns(true);
            var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
            var fileContent = $"{{\"__type\":\"FileLock:#Calamari.Integration.Processes.Semaphores\",\"ProcessId\":{currentProcess.Id},\"ProcessName\":\"{currentProcess.ProcessName}\",\"ThreadId\":{Thread.CurrentThread.ManagedThreadId},\"Timestamp\":636114372739676700}}";
            fileSystem.OpenFileExclusively(lockFilePath, FileMode.Open, FileAccess.Read)
                      .Returns(x => new MemoryStream(Encoding.UTF8.GetBytes(fileContent)));
            var lockFile = (FileLock)lockIo.ReadLock(lockFilePath);
            lockIo.WriteLock(lockFilePath, lockFile);
        }

        [Test]
        public void WriteLockOverwritesLockFileIfTimestampIsDifferent()
        {
            var fileSystem = Substitute.For<ICalamariFileSystem>();
            var lockFilePath = "fake path";
            var lockIo = new LockIo(fileSystem);
            fileSystem.FileExists(lockFilePath).Returns(true);
            var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
            var fileContent = $"{{\"__type\":\"FileLock:#Calamari.Integration.Processes.Semaphores\",\"ProcessId\":{currentProcess.Id},\"ProcessName\":\"{currentProcess.ProcessName}\",\"ThreadId\":{Thread.CurrentThread.ManagedThreadId},\"Timestamp\":636114372739676700}}";
            fileSystem.OpenFileExclusively(lockFilePath, FileMode.Open, FileAccess.Read)
                      .Returns(x => new MemoryStream(Encoding.UTF8.GetBytes(fileContent)));
            var lockFile = (FileLock)lockIo.ReadLock(lockFilePath);
            lockFile.Timestamp = lockFile.Timestamp + 1;
            fileSystem.OpenFileExclusively(lockFilePath, FileMode.Create, FileAccess.Write)
                      .Returns(x => new MemoryStream());
            var result = lockIo.WriteLock(lockFilePath, lockFile);
            Assert.That(result, Is.True);
        }

        [Test]
        public void WriteLockDeletesLockIfUnableToDeserialise()
        {
            var fileSystem = Substitute.For<ICalamariFileSystem>();
            var lockFilePath = "fake path";
            var lockIo = new LockIo(fileSystem);
            fileSystem.FileExists(lockFilePath).Returns(true);
            var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
            var expectedFileContent = $"{{\"__type\":\"FileLock:#Calamari.Integration.Processes.Semaphores\",\"ProcessId\":{currentProcess.Id},\"ProcessName\":\"{currentProcess.ProcessName}\",\"ThreadId\":{Thread.CurrentThread.ManagedThreadId},\"Timestamp\":636114372739676700}}";

            fileSystem.OpenFileExclusively(lockFilePath, FileMode.Open, FileAccess.Read)
                      .Returns(x => new MemoryStream(Encoding.UTF8.GetBytes("non deserialisable content")), x => new MemoryStream(Encoding.UTF8.GetBytes(expectedFileContent)));
            fileSystem.OpenFileExclusively(lockFilePath, FileMode.CreateNew, FileAccess.Write)
                      .Returns(x => new MemoryStream());
            var result = lockIo.WriteLock(lockFilePath, new FileLock(currentProcess.Id, currentProcess.ProcessName, Thread.CurrentThread.ManagedThreadId, 636114372739676700));
            fileSystem.Received().DeleteFile(lockFilePath);
            Assert.That(result, Is.True);
        }

        [Test]
        public void WriteLockReturnsFalseIfIoException()
        {
            var fileSystem = Substitute.For<ICalamariFileSystem>();
            var lockFilePath = "fake path";
            var lockIo = new LockIo(fileSystem);
            fileSystem.FileExists(lockFilePath).Returns(true);
            fileSystem.OpenFileExclusively(lockFilePath, Arg.Any<FileMode>(), Arg.Any<FileAccess>())
                .Returns(x => { throw new IOException("Sharing exception"); });
            var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
            var result = lockIo.WriteLock(lockFilePath, new FileLock(currentProcess.Id, currentProcess.ProcessName, Thread.CurrentThread.ManagedThreadId, 636114372739676700));
            Assert.That(result, Is.False);
        }

        [Test]
        public void WriteLockReturnsFalseIfUnknownException()
        {
            var fileSystem = Substitute.For<ICalamariFileSystem>();
            var lockFilePath = "fake path";
            var lockIo = new LockIo(fileSystem);
            fileSystem.FileExists(lockFilePath).Returns(true);
            fileSystem.OpenFileExclusively(lockFilePath, Arg.Any<FileMode>(), Arg.Any<FileAccess>())
                .Returns(x => { throw new Exception("Unknown exception"); });
            var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
            var result = lockIo.WriteLock(lockFilePath, new FileLock(currentProcess.Id, currentProcess.ProcessName, Thread.CurrentThread.ManagedThreadId, 636114372739676700));
            Assert.That(result, Is.False);
        }
    }
}
