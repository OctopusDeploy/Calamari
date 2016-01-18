﻿using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Calamari.Integration.FileSystem
{
    public class NixCalamariPhysicalFileSystem : CalamariPhysicalFileSystem
    {
        protected override bool GetDiskFreeSpace(string directoryPath, out ulong totalNumberOfFreeBytes)
        {
            // This method will not work for UNC paths on windows 
            // (hence WindowsPhysicalFileSystem) but should be sufficient for Linux mounts
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
