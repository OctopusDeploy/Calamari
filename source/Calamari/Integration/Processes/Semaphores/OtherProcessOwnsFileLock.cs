namespace Calamari.Integration.Processes.Semaphores
{
    public class OtherProcessOwnsFileLock : FileLockContent
    {
        public OtherProcessOwnsFileLock(FileLockContent lockContent)
        {
            ThreadId = lockContent.ThreadId;
            ProcessId = lockContent.ProcessId;
            ProcessName = lockContent.ProcessName;
            Timestamp = lockContent.Timestamp;
        }
    }
}