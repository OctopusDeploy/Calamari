using System;

namespace Calamari.Common.Features.Processes.Semaphores
{
    public class OtherProcessOwnsFileLock : FileLock
    {
        public OtherProcessOwnsFileLock(FileLock fileLock) : base(fileLock.ProcessId, fileLock.ProcessName, fileLock.ThreadId, fileLock.Timestamp)
        {
        }
    }
}