using System;

namespace Calamari.Common.Plumbing.FileSystem
{
    public interface IFreeSpaceChecker
    {
        void EnsureDiskHasEnoughFreeSpace(string directoryPath);
        ulong GetSpaceRequiredToBeFreed(string directoryPath);
        ulong GetRequiredSpaceInBytes();
    }
}