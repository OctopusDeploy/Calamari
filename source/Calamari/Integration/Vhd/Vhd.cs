using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;

namespace Calamari.Integration.Vhd
{
    public class Vhd
    {
        public static void Mount(string vhdPath, string mountPoint)
        {
            var accessMask = VIRTUAL_DISK_ACCESS_MASK.VIRTUAL_DISK_ACCESS_ALL;
            var handle = Open(vhdPath, accessMask);

            // once we have successfully opened we must close the handle
            try
            {
                var attachParameters = new ATTACH_VIRTUAL_DISK_PARAMETERS
                {
                    version = ATTACH_VIRTUAL_DISK_VERSION.ATTACH_VIRTUAL_DISK_VERSION_1
                };

                //Attach but don't give a drive letter
                var attachResult = AttachVirtualDisk(handle, IntPtr.Zero,
                    ATTACH_VIRTUAL_DISK_FLAG.ATTACH_VIRTUAL_DISK_FLAG_PERMANENT_LIFETIME | ATTACH_VIRTUAL_DISK_FLAG.ATTACH_VIRTUAL_DISK_FLAG_NO_DRIVE_LETTER,
                    0, ref attachParameters, IntPtr.Zero);

                if (attachResult != ERROR_SUCCESS)
                {
                    throw new Exception(
                        $"The VHD cannot be accessed [AttachVirtualDisk failed], errorCode: {attachResult}");
                }

                //We need the volume path to mount it
                var vhdVolumePath = FindVhdVolumePath(handle);
                if (!Directory.Exists(mountPoint))
                {
                    Directory.CreateDirectory(mountPoint);
                }
                
                MountVhdVolumeToPath(vhdVolumePath, mountPoint);
            }
            finally
            {
                CloseHandle(handle);
            }
        }

        public static void Unmount(string vhdPath)
        {
            var handle = Open(vhdPath, VIRTUAL_DISK_ACCESS_MASK.VIRTUAL_DISK_ACCESS_DETACH);

            try
            {
                var detachResult = DetachVirtualDisk(handle, DETACH_VIRTUAL_DISK_FLAG.DETACH_VIRTUAL_DISK_FLAG_NONE, 0);
                if (detachResult != ERROR_SUCCESS && detachResult != ERROR_NOT_READY) // get ERROR_NOT_READY if device isn't mounted such us doing a rollback before Mount ran
                {
                    throw new Exception($"The VHD cannot be detached [DetachVirtualDisk failed], errorCode: {detachResult}");
                }
            }
            finally
            {
                CloseHandle(handle);
            }
        }

        private static IntPtr Open(string vhdPath, VIRTUAL_DISK_ACCESS_MASK accessMark)
        {
            var handle = IntPtr.Zero;

            var openParameters = new OPEN_VIRTUAL_DISK_PARAMETERS
            {
                version = OPEN_VIRTUAL_DISK_VERSION.OPEN_VIRTUAL_DISK_VERSION_1,
                version1 = {rwDepth = OPEN_VIRTUAL_DISK_RW_DEPTH_DEFAULT}
            };

            var openStorageType = new VIRTUAL_STORAGE_TYPE
            {
                deviceId = VIRTUAL_STORAGE_TYPE_DEVICE_VHD,
                vendorId = VIRTUAL_STORAGE_TYPE_VENDOR_MICROSOFT
            };

            var openResult = OpenVirtualDisk(ref openStorageType, vhdPath, accessMark,
                OPEN_VIRTUAL_DISK_FLAG.OPEN_VIRTUAL_DISK_FLAG_NONE, ref openParameters, ref handle);
            if (openResult != ERROR_SUCCESS)
            {
                throw new Exception($"The VHD cannot be accessed [OpenVirtualDisk failed], errorCode: {openResult}");
            }
            return handle;
        }

        private static string FindVhdVolumePath(IntPtr vhdHandle)
        {
            //This gets us a drive number, but we need volume path to mount, so we still have some work to do
            var vhdPhysicalDrive = FindVhdPhysicalDriveNumber(vhdHandle);
            var volumeName = new StringBuilder(260);
            var deviceNumber = new STORAGE_DEVICE_NUMBER();
            uint bytesReturned = 0;
            var found = false;

            var findVolumeHandle = FindFirstVolume(volumeName, volumeName.Capacity);
            do
            {
                var backslashPos = volumeName.Length - 1;
                if (volumeName[backslashPos] == '\\')
                {
                    volumeName.Length--;
                }
                var volumeHandle = CreateFile(volumeName.ToString(), 0, FILE_SHARE_MODE_FLAGS.FILE_SHARE_READ | FILE_SHARE_MODE_FLAGS.FILE_SHARE_WRITE, IntPtr.Zero, CREATION_DISPOSITION_FLAGS.OPEN_EXISTING, 0, IntPtr.Zero);
                if (volumeHandle == INVALID_HANDLE_VALUE)
                {
                    continue;
                }

                DeviceIoControl(volumeHandle, IO_CONTROL_CODE.STORAGE_DEVICE_NUMBER, IntPtr.Zero, 0, ref deviceNumber, (uint)Marshal.SizeOf(deviceNumber), ref bytesReturned, IntPtr.Zero);

                if (deviceNumber.deviceNumber == vhdPhysicalDrive)
                {
                    found = true;
                    break;
                }
            } while (FindNextVolume(findVolumeHandle, volumeName, volumeName.Capacity));

            FindVolumeClose(findVolumeHandle);

            if (!found)
            {
                throw new Exception("The VHD cannot be accessed, unable to find the Volume Path");
            }

            return volumeName.ToString();
        }

        private static int FindVhdPhysicalDriveNumber(IntPtr vhdHandle)
        {
            int driveNumber;
            var bufferSize = 260;
            var vhdPhysicalPath = new StringBuilder(bufferSize);

            var result = GetVirtualDiskPhysicalPath(vhdHandle, ref bufferSize, vhdPhysicalPath);
            if (result != ERROR_SUCCESS)
            {
                throw new Exception($"The VHD cannot be accessed [GetVirtualDiskPhysicalPath failed], errorCode: {result}");
            }

            //GetVDPP gives us something like \\.\PhysicalDrive1 we just need the number
            Int32.TryParse(Regex.Match(vhdPhysicalPath.ToString(), @"\d+").Value, out driveNumber);
            return driveNumber;
        }

        private static void MountVhdVolumeToPath(string vhdVolumePath, string mountPoint)
        {
            if (vhdVolumePath[vhdVolumePath.Length - 1] != '\\')
            {
                vhdVolumePath += '\\';
            }

            if (mountPoint[mountPoint.Length - 1] != '\\')
            {
                mountPoint += '\\';
            }

            var setMountPointResult = SetVolumeMountPoint(mountPoint, vhdVolumePath);
            if (!setMountPointResult)
            {
                var error = GetLastError();
                throw new Exception($"The VHD cannot be accessed [SetVolumeMountPoint failed], errorCode: {error}");
            }
        }

        private const Int32 ERROR_SUCCESS = 0;
        private const Int32 ERROR_NOT_READY = 21;
        private const int OPEN_VIRTUAL_DISK_RW_DEPTH_DEFAULT = 1;
        private const int VIRTUAL_STORAGE_TYPE_DEVICE_VHD = 2;
        private static IntPtr INVALID_HANDLE_VALUE = (IntPtr)(-1);

        private static readonly Guid VIRTUAL_STORAGE_TYPE_VENDOR_MICROSOFT = new Guid("EC984AEC-A0F9-47e9-901F-71415A66345B");

        private enum IO_CONTROL_CODE : uint
        {
            GET_VOLUME_DISK_EXTENTS = 5636096,
            STORAGE_DEVICE_NUMBER = 2953344
        }

        private enum ATTACH_VIRTUAL_DISK_FLAG : int
        {
            ATTACH_VIRTUAL_DISK_FLAG_NONE = 0x00000000,
            ATTACH_VIRTUAL_DISK_FLAG_READ_ONLY = 0x00000001,
            ATTACH_VIRTUAL_DISK_FLAG_NO_DRIVE_LETTER = 0x00000002,
            ATTACH_VIRTUAL_DISK_FLAG_PERMANENT_LIFETIME = 0x00000004,
            ATTACH_VIRTUAL_DISK_FLAG_NO_LOCAL_HOST = 0x00000008
        }

        private enum ATTACH_VIRTUAL_DISK_VERSION : int
        {
            ATTACH_VIRTUAL_DISK_VERSION_UNSPECIFIED = 0,
            ATTACH_VIRTUAL_DISK_VERSION_1 = 1
        }

        private enum OPEN_VIRTUAL_DISK_FLAG : int
        {
            OPEN_VIRTUAL_DISK_FLAG_NONE = 0x00000000,
            OPEN_VIRTUAL_DISK_FLAG_NO_PARENTS = 0x00000001,
            OPEN_VIRTUAL_DISK_FLAG_BLANK_FILE = 0x00000002,
            OPEN_VIRTUAL_DISK_FLAG_BOOT_DRIVE = 0x00000004
        }

        private enum OPEN_VIRTUAL_DISK_VERSION : int
        {
            OPEN_VIRTUAL_DISK_VERSION_1 = 1
        }

        private enum VIRTUAL_DISK_ACCESS_MASK : int
        {
            VIRTUAL_DISK_ACCESS_ATTACH_RO = 0x00010000,
            VIRTUAL_DISK_ACCESS_ATTACH_RW = 0x00020000,
            VIRTUAL_DISK_ACCESS_DETACH = 0x00040000,
            VIRTUAL_DISK_ACCESS_GET_INFO = 0x00080000,
            VIRTUAL_DISK_ACCESS_CREATE = 0x00100000,
            VIRTUAL_DISK_ACCESS_METAOPS = 0x00200000,
            VIRTUAL_DISK_ACCESS_READ = 0x000d0000,
            VIRTUAL_DISK_ACCESS_ALL = 0x003f0000,
            VIRTUAL_DISK_ACCESS_WRITABLE = 0x00320000
        }

        private enum DETACH_VIRTUAL_DISK_FLAG : int
        {
            DETACH_VIRTUAL_DISK_FLAG_NONE = 0x00000000
        }

        private enum GET_VIRTUAL_DISK_INFO_VERSION
        {
            GET_VIRTUAL_DISK_INFO_UNSPECIFIED = 0,
            GET_VIRTUAL_DISK_INFO_SIZE = 1,
            GET_VIRTUAL_DISK_INFO_IDENTIFIER = 2,
            GET_VIRTUAL_DISK_INFO_PARENT_LOCATION = 3,
            GET_VIRTUAL_DISK_INFO_PARENT_IDENTIFIER = 4,
            GET_VIRTUAL_DISK_INFO_PARENT_TIMESTAMP = 5,
            GET_VIRTUAL_DISK_INFO_VIRTUAL_STORAGE_TYPE = 6,
            GET_VIRTUAL_DISK_INFO_PROVIDER_SUBTYPE = 7,
            GET_VIRTUAL_DISK_INFO_IS_4K_ALIGNED = 8,
            GET_VIRTUAL_DISK_INFO_PHYSICAL_DISK = 9,
            GET_VIRTUAL_DISK_INFO_VHD_PHYSICAL_SECTOR_SIZE = 10, // 0xA
            GET_VIRTUAL_DISK_INFO_SMALLEST_SAFE_VIRTUAL_SIZE = 11,
            GET_VIRTUAL_DISK_INFO_FRAGMENTATION = 12
        }

        private enum GENERIC_ACCESS_RIGHTS_FLAGS : uint
        {
            GENERIC_READ = 0x80000000,
            GENERIC_WRITE = 0x40000000,
            GENERIC_EXECUTE = 0x20000000,
            GENERIC_ALL = 0x10000000
        }

        private enum FILE_SHARE_MODE_FLAGS : int
        {
            FILE_SHARE_READ = 0x00000001,
            FILE_SHARE_WRITE = 0x00000002
        }

        private enum CREATION_DISPOSITION_FLAGS : int
        {
            CREATE_NEW = 1,
            CREATE_ALWAYS = 2,
            OPEN_EXISTING = 3,
            OPEN_ALWAYS = 4,
            TRUNCATE_EXISTING = 5
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct ATTACH_VIRTUAL_DISK_PARAMETERS
        {
            public ATTACH_VIRTUAL_DISK_VERSION version;
            public ATTACH_VIRTUAL_DISK_PARAMETERS_Version1 version1;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct ATTACH_VIRTUAL_DISK_PARAMETERS_Version1
        {
            public Int32 reserved;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct OPEN_VIRTUAL_DISK_PARAMETERS
        {
            public OPEN_VIRTUAL_DISK_VERSION version;
            public OPEN_VIRTUAL_DISK_PARAMETERS_Version1 version1;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct OPEN_VIRTUAL_DISK_PARAMETERS_Version1
        {
            public Int32 rwDepth;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct VIRTUAL_STORAGE_TYPE
        {
            public Int32 deviceId;
            public Guid vendorId;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct STORAGE_DEVICE_NUMBER
        {
            public Int32 deviceType;
            public Int32 deviceNumber;
            public Int32 partitionNumber;
        }

        [DllImport("virtdisk.dll", CharSet = CharSet.Unicode)]
        private static extern Int32 AttachVirtualDisk(IntPtr virtualDiskHandle, IntPtr securityDescriptor, ATTACH_VIRTUAL_DISK_FLAG flags, Int32 providerSpecificFlags, ref ATTACH_VIRTUAL_DISK_PARAMETERS parameters, IntPtr overlapped);

        [DllImport("virtdisk.dll", CharSet = CharSet.Unicode)]
        private static extern Int32 DetachVirtualDisk(IntPtr virtualDiskHandle, DETACH_VIRTUAL_DISK_FLAG flags, Int32 providerSpecificFlags);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern Boolean CloseHandle(IntPtr hObject);

        [DllImport("virtdisk.dll", CharSet = CharSet.Unicode)]
        private static extern Int32 OpenVirtualDisk(ref VIRTUAL_STORAGE_TYPE virtualStorageType, String path, VIRTUAL_DISK_ACCESS_MASK virtualDiskAccessMask, OPEN_VIRTUAL_DISK_FLAG flags, ref OPEN_VIRTUAL_DISK_PARAMETERS parameters, ref IntPtr handle);

        [DllImport("virtdisk.dll", CharSet = CharSet.Unicode)]
        private static extern Int32 GetVirtualDiskPhysicalPath(IntPtr virtualDiskHandle, ref Int32 diskPathSizeInBytes, [MarshalAs(UnmanagedType.LPWStr)] StringBuilder diskPath);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindFirstVolume([MarshalAs(UnmanagedType.LPTStr)] StringBuilder volumeName, Int32 bufferLength);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FindVolumeClose(IntPtr findVolumeHandle);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FindNextVolume(IntPtr findVolumeHandle, [MarshalAs(UnmanagedType.LPTStr)] StringBuilder volumeName, Int32 bufferLength);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateFile([MarshalAs(UnmanagedType.LPTStr)] string fileName, GENERIC_ACCESS_RIGHTS_FLAGS desiredAccess, FILE_SHARE_MODE_FLAGS shareMode, IntPtr securityAttribute, CREATION_DISPOSITION_FLAGS creationDisposition, Int32 flagsAndAttributes, IntPtr templateFile);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeviceIoControl(IntPtr deviceHandle, IO_CONTROL_CODE controlCode, IntPtr inBuffer, uint inBufferSize, ref STORAGE_DEVICE_NUMBER outBuffer, uint outBufferSize, ref uint bytesReturned, IntPtr overlapped);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetVolumeMountPoint([MarshalAs(UnmanagedType.LPTStr)] string mountPoint, [MarshalAs(UnmanagedType.LPTStr)] string volumeName);

        [DllImport("kernel32.dll")]
        static extern uint GetLastError();

        public static string FindSingleVhdInFolder(ICalamariFileSystem fileSystem, string directory)
        {
            var vhds = fileSystem.EnumerateFiles(directory, "*.vhd", "*.vhdx").ToArray();
            if (vhds.Length == 0)
            {
                throw new Exception("No VHD or VHDX file found in package.");
            }
            if (vhds.Length > 1)
            {
                throw new Exception("More than one VHD or VHDX file found in package. Only bundle a single disk image per package.");
            }
            return vhds.Single();
        }

        public static bool FeatureIsOn(RunningDeployment deployment)
        {
            return deployment.Variables.GetStrings(SpecialVariables.Package.EnabledFeatures).Where(s => !string.IsNullOrWhiteSpace(s)).Contains(SpecialVariables.Features.Vhd);
        }

        public static string GetMountPoint(RunningDeployment deployment)
        {
            return Path.Combine(deployment.CurrentDirectory, "mount");
        }
    }
}