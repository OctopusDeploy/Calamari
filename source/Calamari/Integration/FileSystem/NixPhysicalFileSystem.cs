using System.IO;

namespace Calamari.Integration.FileSystem
{
    public class NixCalamariPhysicalFileSystem : CalamariPhysicalFileSystem
    {
        protected override bool GetFiskFreeSpace(string directoryPath, out ulong totalNumberOfFreeBytes)
        {
            var pathRoot = Path.GetPathRoot(directoryPath);
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (!drive.Name.Equals(pathRoot)) continue;
                totalNumberOfFreeBytes = (ulong)drive.TotalFreeSpace;
                return true;
            }
            totalNumberOfFreeBytes = 0;
            return false;
        }
    }
}
