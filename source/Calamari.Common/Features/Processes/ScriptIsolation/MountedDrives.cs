using System;
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

    /// <summary>
    /// Returns the <see cref="CachedDriveInfo"/> whose mount root is the longest
    /// ancestor of <paramref name="path"/>.
    /// </summary>
    /// <remarks>
    /// Symlinks in <paramref name="path"/> are resolved before matching so that,
    /// for example, <c>/tmp/foo</c> on macOS (where <c>/tmp</c> → <c>/private/tmp</c>)
    /// is correctly matched to the drive that owns <c>/private/tmp</c>.
    /// Path case is compared using the host-filesystem-appropriate
    /// <see cref="StringComparison"/> supplied by <see cref="DefaultPathResolutionService"/>.
    /// </remarks>
    public CachedDriveInfo GetAssociatedDrive(string path)
        => GetAssociatedDrive(path, DefaultPathResolutionService.Instance);

    internal CachedDriveInfo GetAssociatedDrive(string path, IPathResolutionService resolver)
    {
        var resolvedPath = resolver.ResolvePath(path);

        var result = Drives
                         .Where(d => IsAncestor(d.RootDirectory, resolvedPath, resolver.PathComparison))
                         .OrderByDescending(d => d.RootDirectory.FullName.Length)
                         .FirstOrDefault();
        if (result is not null)
        {
            return result;
        }

        throw new DirectoryNotFoundException($"Unable to find the drive for '{path}'.");
    }

    static bool IsAncestor(DirectoryInfo ancestor, string resolvedPath, StringComparison comparison)
    {
        var ancestorPath = ancestor.FullName;
        if (!ancestorPath.EndsWith(Path.DirectorySeparatorChar))
        {
            ancestorPath += Path.DirectorySeparatorChar;
        }
        return resolvedPath.StartsWith(ancestorPath, comparison);
    }
}
