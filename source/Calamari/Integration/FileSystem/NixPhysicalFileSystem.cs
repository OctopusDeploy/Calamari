﻿using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Calamari.Integration.FileSystem
{
    public class NixCalamariPhysicalFileSystem : CalamariPhysicalFileSystem
    {
        public override IEnumerable<string> EnumerateFiles(string parentDirectoryPath, params string[] searchPatterns)
        {
            searchPatterns = searchPatterns.Select(sp => sp.Replace("\\", "/")).ToArray();
            return base.EnumerateFiles(parentDirectoryPath, searchPatterns);
        }

        public override IEnumerable<string> EnumerateFilesRecursively(string parentDirectoryPath, params string[] searchPatterns)
        {
            searchPatterns = searchPatterns.Select(sp => sp.Replace("\\", "/")).ToArray();
            return base.EnumerateFilesRecursively(parentDirectoryPath, searchPatterns);
        }

        protected override bool GetFiskFreeSpace(string directoryPath, out ulong totalNumberOfFreeBytes)
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
