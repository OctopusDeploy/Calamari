using System;

namespace Calamari.Common.Features.Processes.Semaphores
{
    public class UnableToDeserialiseLockFile : FileLock
    {
        public DateTime CreationTime { get; set; }

        public UnableToDeserialiseLockFile(DateTime creationTime)
        {
            CreationTime = creationTime;
        }
    }
}