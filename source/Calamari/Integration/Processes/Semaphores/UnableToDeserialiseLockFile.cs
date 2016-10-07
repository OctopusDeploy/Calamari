using System;

namespace Calamari.Integration.Processes.Semaphores
{
    internal class UnableToDeserialiseLockFile : FileLockContent
    {
        public DateTime CreationTime { get; set; }

        public UnableToDeserialiseLockFile(DateTime creationTime)
        {
            CreationTime = creationTime;
        }
    }
}