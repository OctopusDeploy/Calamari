using System.IO;
using System.Linq;

namespace Calamari.Common.Features.Processes.ScriptIsolation;

sealed record MountedDrives(CachedDriveInfo[] Drives)
{
    public static MountedDrives Get()
    {
        try
        {
            var drives = DriveInfo.GetDrives()
                                  .Select(CachedDriveInfo.From)
                                  .OrderBy(d => d.RootDirectory.FullName)
                                  .ToArray();
            return new MountedDrives(drives);
        }
        catch
        {
            // Let's think about what we really want to do if this happens
            return new MountedDrives([]);
        }
    }

    public CachedDriveInfo GetAssociatedDrive(string path)
    {
        var result = Drives
                         .Where(d => IsAncestor(d.RootDirectory, path))
                         .OrderByDescending(d => d.RootDirectory.FullName.Length)
                         .FirstOrDefault();
        if (result is not null)
        {
            return result;
        }

        throw new DirectoryNotFoundException($"Unable to find the drive for '{path}'.");
    }

    static bool IsAncestor(DirectoryInfo ancestor, string path)
    {
        var ancestorPath = ancestor.FullName;
        if (!ancestorPath.EndsWith(Path.DirectorySeparatorChar))
        {
            ancestorPath += Path.DirectorySeparatorChar;
        }
        return path.StartsWith(ancestorPath, System.StringComparison.OrdinalIgnoreCase);
    }
}
