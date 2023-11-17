using System;
using System.Runtime.InteropServices;

namespace Calamari.Common.Plumbing.FileSystem
{
    public class WindowsPhysicalFileSystem : CalamariPhysicalFileSystem
    {
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetDiskFreeSpaceEx(string lpDirectoryName, out ulong lpFreeBytesAvailable, out ulong lpTotalNumberOfBytes, out ulong lpTotalNumberOfFreeBytes);

        public override bool GetDiskFreeSpace(string directoryPath, out ulong totalNumberOfFreeBytes)
        {
            return GetDiskFreeSpaceEx(directoryPath, out var _, out var __, out totalNumberOfFreeBytes);
        }

        public override bool GetDiskTotalSpace(string directoryPath, out ulong totalNumberOfBytes)
        {
            return GetDiskFreeSpaceEx(directoryPath, out var _, out totalNumberOfBytes, out var __);
        }
    }
}