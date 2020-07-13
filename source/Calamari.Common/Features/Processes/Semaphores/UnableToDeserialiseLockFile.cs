using System;

namespace Calamari.Common.Features.Processes.Semaphores
{
    public class UnableToDeserialiseLockFile : FileLock
    {
        public UnableToDeserialiseLockFile(DateTime creationTime)
        {
            CreationTime = creationTime;
        }

        public DateTime CreationTime { get; set; }
    }
}