using System;

namespace Calamari.Common.Features.Processes.Semaphores
{
    public interface ILockIo
    {
        string GetFilePath(string lockName);
        bool LockExists(string lockFilePath);
        IFileLock ReadLock(string lockFilePath);
        bool WriteLock(string lockFilePath, FileLock fileLock);
        void DeleteLock(string lockFilePath);
    }
}