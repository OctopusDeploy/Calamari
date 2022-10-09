using Calamari.Tests.Fixtures.Integration.FileSystem;

namespace Calamari.Tests.Fixtures.PackageRetention
{
    public class FileSystemThatHasSpace : TestCalamariPhysicalFileSystem
    {
        readonly ulong freeBytes;
        readonly ulong totalBytes;

        public FileSystemThatHasSpace(ulong freeBytes, ulong totalBytes)
        {
            this.freeBytes = freeBytes;
            this.totalBytes = totalBytes;
        }

        public override bool GetDiskFreeSpace(string directoryPath, out ulong totalNumberOfFreeBytes)
        {
            totalNumberOfFreeBytes = freeBytes;
            return true;
        }

        public override bool GetDiskTotalSpace(string directoryPath, out ulong totalNumberOfFreeBytes)
        {
            totalNumberOfFreeBytes = totalBytes;
            return true;
        }
    }
}