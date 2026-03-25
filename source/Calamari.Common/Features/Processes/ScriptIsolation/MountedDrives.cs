using System;
using System.IO;
using System.Linq;

namespace Calamari.Common.Features.Processes.ScriptIsolation;

sealed class MountedDrives(
    CachedDriveInfo[] drives,
    IPathResolutionService pathResolutionService
) : ICachedDriveInfoProvider
{
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
    {
        var resolvedPath = pathResolutionService.ResolvePath(path);

        var result = drives
                         .Where(d => IsAncestor(d.RootDirectory, resolvedPath))
                         .OrderByDescending(d => d.RootDirectory.FullName.Length)
                         .FirstOrDefault();
        if (result is not null)
        {
            return result;
        }

        throw new DirectoryNotFoundException($"Unable to find the drive for '{path}'.");
    }

    public bool IsAncestor(DirectoryInfo ancestor, string resolvedPath)
    {
        var ancestorPath = ancestor.FullName;
        if (!ancestorPath.EndsWith(Path.DirectorySeparatorChar))
        {
            ancestorPath += Path.DirectorySeparatorChar;
        }
        return resolvedPath.StartsWith(ancestorPath, pathResolutionService.PathComparison);
    }
}
