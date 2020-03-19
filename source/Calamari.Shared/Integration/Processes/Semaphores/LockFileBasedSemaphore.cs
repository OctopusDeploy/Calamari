using System;
using System.Diagnostics;
using System.Threading;
using Calamari.Integration.FileSystem;

namespace Calamari.Integration.Processes.Semaphores
{
    public class LockFileBasedSemaphore : ISemaphore
    {
        private readonly ILockIo lockIo;
        private readonly IProcessFinder processFinder;
        private readonly ILog log;

        public LockFileBasedSemaphore(string name, TimeSpan lockTimeout) 
            : this(name, lockTimeout, new LockIo(CalamariPhysicalFileSystem.GetPhysicalFileSystem()), new ProcessFinder())
        {
        }

        public LockFileBasedSemaphore(string name, TimeSpan lockTimeout, ILockIo lockIo, IProcessFinder processFinder)
            : this(name, lockTimeout, lockIo, processFinder, ConsoleLog.Instance)
        {
        }
        public LockFileBasedSemaphore(string name, TimeSpan lockTimeout, ILockIo lockIo, IProcessFinder processFinder, ILog log)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name), "name cannot be null or emtpy.");

            Name = name;
            LockTimeout = lockTimeout;
            LockFilePath = lockIo.GetFilePath(name);
            this.lockIo = lockIo;
            this.processFinder = processFinder;
            this.log = log;
        }

        private TimeSpan LockTimeout { get; }

        public string Name { get; set; }

        private string LockFilePath { get; }

        public AquireLockAction ShouldAquireLock(FileLock fileLock)
        {
            if (lockIo.LockExists(LockFilePath))
            {
                //Someone else owns the lock
                if (fileLock.GetType() == typeof(OtherProcessHasExclusiveLockOnFileLock))
                {
                    //we couldn't read the file as some other process has it open exlusively
                    return AquireLockAction.DontAquireLock;
                }

                if (fileLock.GetType() == typeof(UnableToDeserialiseLockFile))
                {
                    var nonDeserialisedLockFile = (UnableToDeserialiseLockFile)fileLock;
                    if ((DateTime.Now - nonDeserialisedLockFile.CreationTime).TotalSeconds > LockTimeout.TotalSeconds)
                    {
                        log.Warn("Lock file existed but was not readable, and has existed for longer than lock timeout. Taking lock.");
                        return AquireLockAction.AquireLock;
                    }
                    return AquireLockAction.DontAquireLock;
                }

                //the file no longer exists
                if (fileLock.GetType() == typeof(MissingFileLock))
                {
                    return AquireLockAction.AquireLock;
                }

                //This lock belongs to this process - we can reacquire the lock
                if (fileLock.BelongsToCurrentProcessAndThread())
                {
                    return AquireLockAction.AquireLock;
                }

                if (!processFinder.ProcessIsRunning((int)fileLock.ProcessId, fileLock.ProcessName))
                {
                    log.Warn($"Process {fileLock.ProcessId}, thread {fileLock.ThreadId} had lock, but appears to have crashed. Taking lock.");

                    return AquireLockAction.AquireLock;
                }

                var lockWriteTime = new DateTime(fileLock.Timestamp);
                //The lock has not timed out - we can't acquire it
                if (!(Math.Abs((DateTime.Now - lockWriteTime).TotalSeconds) > LockTimeout.TotalSeconds))
                {
                    return AquireLockAction.DontAquireLock;
                }

                log.Warn($"Forcibly taking lock from process {fileLock.ProcessId}, thread {fileLock.ThreadId} as lock has timed out. If this happens regularly, please contact Octopus Support.");

                return AquireLockAction.ForciblyAquireLock;
            }

            return AquireLockAction.AquireLock;
        }

        public bool TryAcquireLock()
        {
            var lockContent = lockIo.ReadLock(LockFilePath);
            var response = ShouldAquireLock(lockContent);
            if (response == AquireLockAction.ForciblyAquireLock)
                lockIo.DeleteLock(LockFilePath);

            if (response == AquireLockAction.AquireLock || response == AquireLockAction.ForciblyAquireLock)
                return lockIo.WriteLock(LockFilePath, CreateLockContent());

            return false;
        }

        public void ReleaseLock()
        {
            //Need to own the lock in order to release it (and we can reacquire the lock inside the current process)
            if (lockIo.LockExists(LockFilePath) && TryAcquireLock())
                lockIo.DeleteLock(LockFilePath);
        }

        private static FileLock CreateLockContent()
        {
            var process = Process.GetCurrentProcess();
            return new FileLock
            {
                ProcessId = process.Id,
                Timestamp = DateTime.Now.Ticks,
                ProcessName = process.ProcessName,
                ThreadId = Thread.CurrentThread.ManagedThreadId
            };
        }

        public bool WaitOne(int millisecondsToWait)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            while (true)
            {
                if (TryAcquireLock())
                    return true;
                if (stopwatch.ElapsedMilliseconds > millisecondsToWait)
                    return false;
                Thread.Sleep(100);
            }
        }

        public bool WaitOne()
        {
            while (true)
            {
                if (TryAcquireLock())
                    return true;
                Thread.Sleep(100);
            }
        }

        public enum AquireLockAction
        {
            DontAquireLock,
            AquireLock,
            ForciblyAquireLock
        }
    }
}