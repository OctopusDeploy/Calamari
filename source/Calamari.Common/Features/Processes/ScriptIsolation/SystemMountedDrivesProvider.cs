using System.IO;
using System.Linq;

namespace Calamari.Common.Features.Processes.ScriptIsolation;

class SystemMountedDrivesProvider(
    IPathResolutionService pathResolutionService
) : IMountedDrivesProvider
{
    public MountedDrives GetMountedDrives()
    {
        try
        {
            var drives = DriveInfo.GetDrives()
                                  .Select(CachedDriveInfo.From)
                                  .OrderBy(d => d.RootDirectory.FullName)
                                  .ToArray();
            return new MountedDrives(drives, pathResolutionService);
        }
        catch
        {
            // Let's think about what we really want to do if this happens
            return new MountedDrives([], pathResolutionService);
        }
    }
}
