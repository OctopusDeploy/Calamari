namespace Calamari.Integration.Processes.Semaphores
{
    public interface ILockIo
    {
        string GetFilePath(string lockName);
        bool LockExists(string lockFilePath);
        FileLock ReadLock(string lockFilePath);
        bool WriteLock(string lockFilePath, FileLock fileLock);
        void DeleteLock(string lockFilePath);
    }
}