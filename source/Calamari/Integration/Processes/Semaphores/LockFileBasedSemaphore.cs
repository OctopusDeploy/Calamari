using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace Calamari.Integration.Processes.Semaphores
{
    class LockFileBasedSemaphore
    {
        protected LockFileBasedSemaphore(string name, TimeSpan lockTimeout)
        {
            Name = name;
            LockTimeout = lockTimeout;
        }

        private TimeSpan LockTimeout { get; set; }

        internal string Name { get; set; }

        private string LockFilePath { get; set; }

        private bool TryAcquireLock()
        {
            if (LockIo.LockExists(LockFilePath))
            {
                var lockContent = LockIo.ReadLock(LockFilePath);

                //Someone else owns the lock
                if (lockContent.GetType() == typeof(OtherProcessHasExclusiveLockOnFileLock))
                {
                    //we couldn't read the file as some other process has it open exlusively
                    return false;
                }

                if (lockContent.GetType() == typeof(UnableToDeserialiseLockFile))
                {
                    var nonDeserialisedLockFile = (UnableToDeserialiseLockFile) lockContent;
                    if ((DateTime.Now - nonDeserialisedLockFile.CreationTime).TotalSeconds > LockTimeout.TotalSeconds)
                    {
                        Log.Warn($"Lock file existed but was not readable, and has existed for longer than lock timeout. Taking lock.");
                        return AcquireLock();
                    }
                    Console.WriteLine($"{Process.GetCurrentProcess().Id}/{Thread.CurrentThread.ManagedThreadId} - Lock file existed, but was not readable. Giving up in {(LockTimeout.TotalSeconds - (Math.Abs((DateTime.Now - nonDeserialisedLockFile.CreationTime).TotalSeconds))):##} seconds.");
                    return false;
                }

                //the file no longer exists
                if (lockContent.GetType() == typeof(MissingFileLock))
                {
                    return AcquireLock();
                }

                //This lock belongs to this process - we can reacquire the lock
                if (lockContent.ProcessId == Process.GetCurrentProcess().Id && lockContent.ThreadId == Thread.CurrentThread.ManagedThreadId)
                {
                    return AcquireLock();
                }

                if (!ProcessIsRunning((int)lockContent.ProcessId, lockContent.ProcessName))
                {
                    Log.Warn($"Process {lockContent.ProcessId}, thread {lockContent.ThreadId} had lock, but appears to have crashed. Taking lock.");

                    return AcquireLock();
                }

                var lockWriteTime = new DateTime(lockContent.Timestamp);
                //The lock has not timed out - we can't acquire it
                if (!(Math.Abs((DateTime.Now - lockWriteTime).TotalSeconds) > LockTimeout.TotalSeconds))
                {
                    Console.WriteLine($"{Process.GetCurrentProcess().Id}/{Thread.CurrentThread.ManagedThreadId} - Lock times out in {(LockTimeout.TotalSeconds - (Math.Abs((DateTime.Now - lockWriteTime).TotalSeconds))):##} seconds");
                    return false;
                }

                Console.WriteLine($"{Process.GetCurrentProcess().Id}/{Thread.CurrentThread.ManagedThreadId} - Force deleting file based semaphore (timeout)");
                Log.Warn($"Forcibly taking lock from process {lockContent.ProcessId}, thread {lockContent.ThreadId} as lock has timed out. If this happens regularly, please contact Octopus Support.");

                LockIo.DeleteLock(LockFilePath);
            }

            //Acquire the lock
            return AcquireLock();
        }

        internal bool ReleaseLock()
        {
            //Need to own the lock in order to release it (and we can reacquire the lock inside the current process)
            if (LockIo.LockExists(LockFilePath) && TryAcquireLock())
                LockIo.DeleteLock(LockFilePath);
            return true;
        }

        #region Internal methods

        private FileLockContent CreateLockContent()
        {
            var process = Process.GetCurrentProcess();
            return new FileLockContent()
            {
                ProcessId = process.Id,
                Timestamp = DateTime.Now.Ticks,
                ProcessName = process.ProcessName,
                ThreadId = Thread.CurrentThread.ManagedThreadId
            };
        }

        private static bool ProcessIsRunning(int processId, string processName)
        {
            // borrowed from https://github.com/sillsdev/libpalaso/blob/7c7e5eed0a3d9c8a961b01887cbdebbf1b63b899/SIL.Core/IO/FileLock/SimpleFileLock.cs
            // (Apache 2.0 license)
            // First, look for a process with this processId
            Process process;
            try
            {
                process = Process.GetProcesses().FirstOrDefault(x => x.Id == processId);
            }
            catch (NotSupportedException)
            {
                //FreeBSD does not support EnumProcesses
                //assume that the process is running
                return true;
            }

            // If there is no process with this processId, it is not running.
            if (process == null)
                return false;

            // Next, check for a match on processName.
            var isRunning = process.ProcessName == processName;

            // If a match was found or this is running on Windows, this is as far as we need to go.
            if (isRunning || CalamariEnvironment.IsRunningOnWindows)
                return isRunning;

            // We need to look a little deeper on Linux.

            // If the name of the process is not "mono" or does not start with "mono-", this is not
            // a mono application, and therefore this is not the process we are looking for.
            if (process.ProcessName.ToLower() != "mono" && !process.ProcessName.ToLower().StartsWith("mono-"))
                return false;

            // The mono application will have a module with the same name as the process, with ".exe" added.
            var moduleName = processName.ToLower() + ".exe";
            return process.Modules.Cast<ProcessModule>().Any(mod => mod.ModuleName.ToLower() == moduleName);
        }

        private bool AcquireLock()
        {
            return LockIo.WriteLock(LockFilePath, CreateLockContent());
        }

        #endregion

        #region Create methods

        public static LockFileBasedSemaphore Create(string lockName, TimeSpan lockTimeout)
        {
            if (string.IsNullOrEmpty(lockName))
                throw new ArgumentNullException(nameof(lockName), "lockName cannot be null or emtpy.");

            return new LockFileBasedSemaphore(lockName, lockTimeout) { LockFilePath = LockIo.GetFilePath(lockName) };
        }

        #endregion

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
                Thread.Sleep(1000); //todo: knock back to 100 once tests are sorted
            }
        }

        public bool WaitOne()
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            while (true)
            {
                if (TryAcquireLock())
                    return true;
                Thread.Sleep(1000); //todo: knock back to 100 once tests are sorted
            }
        }
    }
}