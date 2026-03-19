using System;
using System.Collections.Generic;
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
///     to follow symlink chains to the real filesystem location.
///   </description></item>
/// </list>
/// <para>
/// <strong>Non-existent paths:</strong> <see cref="FileSystemInfo.ResolveLinkTarget"/>
/// throws when the entry does not exist on disk — it does <em>not</em> walk up the path
/// to resolve symlink components in ancestor directories.  For example, on macOS where
/// <c>/tmp</c> is a symlink to <c>/private/tmp</c>, passing <c>/tmp/new-dir</c> to
/// <c>ResolveLinkTarget</c> directly would throw and our implementation would fall back
/// to returning the unresolved <c>/tmp/new-dir</c>, causing it to fail to match the
/// <c>/private/tmp</c> drive root.
/// </para>
/// <para>
/// To handle this correctly, <see cref="ResolvePath"/> walks up the path component by
/// component until it finds an ancestor that exists, resolves that ancestor's symlinks,
/// then re-attaches the non-existent tail.  This means <c>/tmp/new-dir</c> is correctly
/// resolved to <c>/private/tmp/new-dir</c> even though <c>new-dir</c> does not yet exist.
/// </para>
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

        // Walk up the path collecting non-existent tail segments until we find an
        // ancestor that exists on disk, then resolve that ancestor's symlinks and
        // re-attach the peeled-off segments.
        var tailSegments = new Stack<string>();
        var current = fullPath;

        while (true)
        {
            try
            {
                var info = new FileInfo(current);
                // ResolveLinkTarget returns null when the entry exists but is not a symlink.
                // It throws when the path does not exist.
                var resolved = info.ResolveLinkTarget(returnFinalTarget: true);
                var resolvedBase = resolved is not null ? resolved.FullName : current;

                // Re-attach any non-existent tail segments we peeled off.
                foreach (var segment in tailSegments)
                    resolvedBase = Path.Combine(resolvedBase, segment);

                return resolvedBase;
            }
            catch (Exception ex) when (ex is FileNotFoundException
                                           or DirectoryNotFoundException
                                           or IOException)
            {
                // current doesn't exist — peel off the last path segment and try its parent.
                var parent = Path.GetDirectoryName(current);
                if (parent is null || parent == current)
                {
                    // Reached the filesystem root without finding anything resolvable.
                    return fullPath;
                }

                tailSegments.Push(Path.GetFileName(current));
                current = parent;
            }
        }
    }

    /// <inheritdoc/>
    public StringComparison PathComparison =>
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
}
