using System;
using System.IO;

namespace Calamari.Common.Features.Processes.ScriptIsolation;

/// <summary>
/// The real, production implementation of <see cref="IPathResolutionService"/>.
/// Delegates entirely to well-tested BCL primitives:
/// <list type="bullet">
///   <item><description>
///     <see cref="Path.GetFullPath"/> to normalise the path and make it absolute.
///   </description></item>
///   <item><description>
///     <see cref="FileSystemInfo.ResolveLinkTarget"/> (with <c>returnFinalTarget: true</c>)
///     to follow symlink chains to the real filesystem location.  Falls back
///     gracefully when the path does not yet exist or is not a symlink.
///   </description></item>
/// </list>
/// </summary>
sealed class DefaultPathResolutionService : IPathResolutionService
{
    DefaultPathResolutionService() { }

    public static readonly DefaultPathResolutionService Instance = new();

    /// <inheritdoc/>
    public string ResolvePath(string path)
    {
        // Start with a fully-qualified, normalised path (handles .., ., relative paths).
        var fullPath = Path.GetFullPath(path);

        try
        {
            // ResolveLinkTarget(returnFinalTarget: true) follows the complete symlink
            // chain to its final target.  It returns null when the entry at fullPath
            // is not itself a symlink (the path is already a real location).
            // It throws IOException when the path does not exist on disk at all.
            var info = new FileInfo(fullPath);
            var resolved = info.ResolveLinkTarget(returnFinalTarget: true);
            return resolved is not null ? resolved.FullName : fullPath;
        }
        catch
        {
            // Path does not exist or cannot be resolved — return the normalised form.
            return fullPath;
        }
    }

    /// <inheritdoc/>
    public StringComparison PathComparison =>
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
}
