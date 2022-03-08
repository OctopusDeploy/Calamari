using System;
using System.Runtime.InteropServices;

namespace Calamari.Common.Plumbing.FileSystem
{
    public class WindowsPhysicalFileSystem : CalamariPhysicalFileSystem
    {
        public WindowsPhysicalFileSystem()
        {
#if USE_ALPHAFS_FOR_LONG_FILE_PATH_SUPPORT
            File = new LongPathsFile();
            Directory = new LongPathsDirectory();
#endif
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetDiskFreeSpaceEx(string lpDirectoryName, out ulong lpFreeBytesAvailable, out ulong lpTotalNumberOfBytes, out ulong lpTotalNumberOfFreeBytes);

        public override bool GetDiskFreeSpace(string directoryPath, out ulong totalNumberOfFreeBytes)
        {
            ulong freeBytesAvailable;
            ulong totalNumberOfBytes;

            return GetDiskFreeSpaceEx(directoryPath, out freeBytesAvailable, out totalNumberOfBytes, out totalNumberOfFreeBytes);
        }

        public override bool GetDiskTotalSpace(string directoryPath, out ulong totalNumberOfBytes)
        {
            ulong freeBytesAvailable;
            ulong totalNumberOfFreeBytes;

            return GetDiskFreeSpaceEx(directoryPath, out freeBytesAvailable, out totalNumberOfBytes, out totalNumberOfFreeBytes);
        }
    }
}