using System.IO;

namespace Calamari.Common.Features.Processes.ScriptIsolation;

sealed record CachedDriveInfo(
    DirectoryInfo RootDirectory,
    string Format,
    DriveType DriveType,
    LockCapability? DetectedLockSupport = null
)
{
    const string UnknownFormat = "unknown";

    public static CachedDriveInfo From(DriveInfo driveInfo)
    {
        // These should not throw
        var rootDirectory = driveInfo.RootDirectory;
        var driveType = driveInfo.DriveType;
        try
        {
            var format = driveInfo.DriveFormat; // May throw
            return new CachedDriveInfo(rootDirectory, format, driveType);
        }
        catch
        {
            // If it is throwing an error here, don't trust it for locking
            return new CachedDriveInfo(
                                       RootDirectory: rootDirectory,
                                       Format: UnknownFormat,
                                       DriveType: driveType,
                                       DetectedLockSupport: LockCapability.Unsupported
                                      );
        }
    }

    public LockCapability? LockSupport
    {
        get
        {
            if (DetectedLockSupport is not null)
            {
                return DetectedLockSupport.Value;
            }

            switch (DriveType)
            {
                case DriveType.Network:
                    return null; // Default to assuming network is unknown
                default:  // Explicitly falling through to format inspection
                    break;
            }

            switch (Format.ToLowerInvariant())
            {
                case "apfs":
                case "btrfs":
                case "ext4":
                case "hfs+":
                case "ntfs":
                case "tmpfs":
                case "xfs":
                case "zfs":
                    // We trust that these filesystems fully support file locking and will skip
                    // testing these filesystems for compatibility.
                    return LockCapability.Supported;
                default:
                    return null;
            }
        }
    }

}
