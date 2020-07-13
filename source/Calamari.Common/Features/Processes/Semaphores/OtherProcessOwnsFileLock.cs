namespace Calamari.Common.Features.Processes.Semaphores
{
    public class OtherProcessOwnsFileLock : FileLock
    {
        public OtherProcessOwnsFileLock(FileLock fileLock)
        {
            ThreadId = fileLock.ThreadId;
            ProcessId = fileLock.ProcessId;
            ProcessName = fileLock.ProcessName;
            Timestamp = fileLock.Timestamp;
        }
    }
}