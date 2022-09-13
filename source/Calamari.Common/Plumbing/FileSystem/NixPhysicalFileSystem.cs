using System;
using System.IO;

namespace Calamari.Common.Plumbing.FileSystem
{
    public class NixCalamariPhysicalFileSystem : CalamariPhysicalFileSystem
    {
        public override bool GetDiskFreeSpace(string directoryPath, out ulong totalNumberOfFreeBytes)
        {
            // This method will not work for UNC paths on windows 
            // (hence WindowsPhysicalFileSystem) but should be sufficient for Linux mounts
            var pathRoot = Path.GetPathRoot(directoryPath);
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (!drive.Name.Equals(pathRoot))
                    continue;
                totalNumberOfFreeBytes = (ulong)drive.TotalFreeSpace;
                return true;
            }

            totalNumberOfFreeBytes = 0;
            return false;
        }

        public override bool GetDiskTotalSpace(string directoryPath, out ulong totalNumberOfBytes)
        {
            var pathRoot = Path.GetPathRoot(directoryPath);
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (!drive.Name.Equals(pathRoot))
                    continue;
                totalNumberOfBytes = (ulong)drive.TotalSize;
                return true;
            }

            totalNumberOfBytes = 0;
            return false;
        }
    }
}