using System;

namespace Calamari.Integration.Processes.Semaphores
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