using System;

namespace Calamari.Integration.Processes.Semaphores
{
    //Originally based on https://github.com/markedup-mobi/file-lock (MIT license)
    public class FileBasedSempahoreManager : ISemaphoreFactory
    {
        private readonly ILog log;
        private readonly ICreateSemaphores semaphoreCreator;
        private readonly int initialWaitBeforeShowingLogMessage;

        public FileBasedSempahoreManager()
        {
            this.log = new LogWrapper();
            this.initialWaitBeforeShowingLogMessage = (int)TimeSpan.FromSeconds(3).TotalMilliseconds;
            this.semaphoreCreator = new LockFileBasedSemaphoreCreator();
        }

        public FileBasedSempahoreManager(ILog log, TimeSpan initialWaitBeforeShowingLogMessage, ICreateSemaphores semaphoreCreator)
        {
            this.log = log;
            this.semaphoreCreator = semaphoreCreator;
            this.initialWaitBeforeShowingLogMessage = (int)initialWaitBeforeShowingLogMessage.TotalMilliseconds;
        }

        public IDisposable Acquire(string name, string waitMessage)
        {
            var semaphore = semaphoreCreator.Create(name, TimeSpan.FromMinutes(2));

            if (!semaphore.WaitOne(initialWaitBeforeShowingLogMessage))
            {
                log.Verbose(waitMessage);
                semaphore.WaitOne();
            }

            return new LockFileBasedSemaphoreReleaser(semaphore);
        }

        private class LockFileBasedSemaphoreReleaser : IDisposable
        {
            private readonly ISemaphore semaphore;

            public LockFileBasedSemaphoreReleaser(ISemaphore semaphore)
            {
                this.semaphore = semaphore;
            }

            public void Dispose()
            {
                semaphore.ReleaseLock();
            }
        }
    }
}