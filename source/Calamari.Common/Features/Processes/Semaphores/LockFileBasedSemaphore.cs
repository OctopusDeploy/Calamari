using System;
using System.Diagnostics;
using System.Threading;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.Common.Features.Processes.Semaphores
{
    public class LockFileBasedSemaphore : ISemaphore
    {
        public enum AcquireLockAction
        {
            DontAcquireLock,
            AcquireLock,
            ForciblyAcquireLock
        }

        readonly ILockIo lockIo;
        readonly IProcessFinder processFinder;
        readonly ILog log;

        public LockFileBasedSemaphore(string name, TimeSpan lockTimeout, ILog log)
            : this(name,
                lockTimeout,
                new LockIo(CalamariPhysicalFileSystem.GetPhysicalFileSystem()),
                new ProcessFinder(),
                log)
        {
        }

        public LockFileBasedSemaphore(string name,
            TimeSpan lockTimeout,
            ILockIo lockIo,
            IProcessFinder processFinder,
            ILog log)
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

        TimeSpan LockTimeout { get; }

        public string Name { get; }

        string LockFilePath { get; }

        public AcquireLockAction ShouldAcquireLock(IFileLock fileLock)
        {
            if (lockIo.LockExists(LockFilePath))
            {
                //Someone else owns the lock
                if (fileLock is OtherProcessHasExclusiveLockOnFileLock)
                    //we couldn't read the file as some other process has it open exlusively
                    return AcquireLockAction.DontAcquireLock;

                if (fileLock is UnableToDeserialiseLockFile nonDeserialisedLockFile)
                {
                    if ((DateTime.Now - nonDeserialisedLockFile.CreationTime).TotalSeconds > LockTimeout.TotalSeconds)
                    {
                        log.Warn("Lock file existed but was not readable, and has existed for longer than lock timeout. Taking lock.");
                        return AcquireLockAction.AcquireLock;
                    }

                    return AcquireLockAction.DontAcquireLock;
                }

                //the file no longer exists
                if (fileLock is MissingFileLock)
                    return AcquireLockAction.AcquireLock;

                var concreteFileLock = fileLock as FileLock;
                if (concreteFileLock == null)
                    return AcquireLockAction.AcquireLock;
                
                //This lock belongs to this process - we can reacquire the lock
                if (concreteFileLock.BelongsToCurrentProcessAndThread())
                    return AcquireLockAction.AcquireLock;

                if (!processFinder.ProcessIsRunning((int)concreteFileLock.ProcessId, concreteFileLock.ProcessName ?? ""))
                {
                    log.Warn($"Process {concreteFileLock.ProcessId}, thread {concreteFileLock.ThreadId} had lock, but appears to have crashed. Taking lock.");

                    return AcquireLockAction.AcquireLock;
                }

                var lockWriteTime = new DateTime(concreteFileLock.Timestamp);
                //The lock has not timed out - we can't acquire it
                if (!(Math.Abs((DateTime.Now - lockWriteTime).TotalSeconds) > LockTimeout.TotalSeconds))
                    return AcquireLockAction.DontAcquireLock;

                log.Warn($"Forcibly taking lock from process {concreteFileLock.ProcessId}, thread {concreteFileLock.ThreadId} as lock has timed out. If this happens regularly, please contact Octopus Support.");

                return AcquireLockAction.ForciblyAcquireLock;
            }

            return AcquireLockAction.AcquireLock;
        }

        public bool TryAcquireLock()
        {
            var lockContent = lockIo.ReadLock(LockFilePath);
            var response = ShouldAcquireLock(lockContent);
            if (response == AcquireLockAction.ForciblyAcquireLock)
                lockIo.DeleteLock(LockFilePath);

            if (response == AcquireLockAction.AcquireLock || response == AcquireLockAction.ForciblyAcquireLock)
                return lockIo.WriteLock(LockFilePath, CreateLockContent());

            return false;
        }

        public void ReleaseLock()
        {
            //Need to own the lock in order to release it (and we can reacquire the lock inside the current process)
            if (lockIo.LockExists(LockFilePath) && TryAcquireLock())
                lockIo.DeleteLock(LockFilePath);
        }

        static FileLock CreateLockContent()
        {
            var process = Process.GetCurrentProcess();
            return new FileLock(process.Id,
                process.ProcessName,
                Thread.CurrentThread.ManagedThreadId,
                DateTime.Now.Ticks);
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
    }
}