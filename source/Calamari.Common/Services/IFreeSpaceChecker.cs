namespace Calamari.Integration.FileSystem
{
    public interface IFreeSpaceChecker
    {
        void EnsureDiskHasEnoughFreeSpace(string directoryPath);
    }
}