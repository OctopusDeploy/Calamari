using System;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.Common.Features.Processes.Semaphores
{
    //Originally based on https://github.com/markedup-mobi/file-lock (MIT license)
    public class FileBasedSempahoreManager : ISemaphoreFactory
    {
        readonly ILog log;
        readonly ICreateSemaphores semaphoreCreator;
        readonly int initialWaitBeforeShowingLogMessage;

        public FileBasedSempahoreManager()
        {
            log = ConsoleLog.Instance;
            initialWaitBeforeShowingLogMessage = (int)TimeSpan.FromSeconds(3).TotalMilliseconds;
            semaphoreCreator = new LockFileBasedSemaphoreCreator(log);
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

        class LockFileBasedSemaphoreReleaser : IDisposable
        {
            readonly ISemaphore semaphore;

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