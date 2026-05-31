using System;
using System.Collections.Generic;
using System.IO;
using System.Security;

namespace Calamari.Common.Features.Processes.ScriptIsolation;

static class PathResolutionServiceExtensions
{
    /// <summary>
    /// Returns the canonical, fully-qualified form of <paramref name="path"/>,
    /// resolving symlinks and any <c>..</c> / <c>.</c> components.
    /// </summary>
    /// <returns>
    /// The resolved, absolute path.  If <see cref="IPathResolutionService.GetFullPath"/>
    /// throws (e.g. due to an invalid path, insufficient permissions, or a path that
    /// exceeds the system maximum length), <paramref name="path"/> is returned
    /// unchanged so that a single malformed path cannot prevent other candidates from
    /// being evaluated.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The method first calls <see cref="IPathResolutionService.GetFullPath"/> to
    /// produce an absolute, lexically-normalised path.
    /// <see cref="IPathResolutionService.GetFullPath"/> may throw
    /// <see cref="ArgumentException"/>, <see cref="ArgumentNullException"/>,
    /// <see cref="SecurityException"/>, <see cref="NotSupportedException"/>, or
    /// <see cref="PathTooLongException"/> for invalid or inaccessible inputs.  All of
    /// these are caught here and cause the method to return <paramref name="path"/>
    /// unmodified.
    /// </para>
    /// <para>
    /// The method then attempts to resolve symlinks via
    /// <see cref="IPathResolutionService.ResolveLinkTarget"/>.
    /// Because <see cref="IPathResolutionService.ResolveLinkTarget"/> throws when the
    /// target path does not exist on disk or is inaccessible, the method walks <em>up</em> the path
    /// component by component until it locates an ancestor that does exist, resolves
    /// that ancestor's symlinks, then re-attaches the non-existent tail segments.
    /// This correctly handles cases such as <c>/tmp/new-dir</c> on macOS (where
    /// <c>/tmp</c> → <c>/private/tmp</c>): even though <c>new-dir</c> does not yet
    /// exist, the result is <c>/private/tmp/new-dir</c>.
    /// </para>
    /// <para>
    /// If no resolvable ancestor can be found (i.e. we reach the filesystem root
    /// without finding an existing entry), the normalised but unresolved path from
    /// <see cref="IPathResolutionService.GetFullPath"/> is returned as a safe fallback.
    /// </para>
    /// </remarks>
    public static string ResolvePath(this IPathResolutionService resolver, string path)
    {
        // Start with a fully-qualified, normalised path (handles .., ., relative paths).
        // GetFullPath may throw for invalid inputs (ArgumentException, SecurityException,
        // ArgumentNullException, NotSupportedException, PathTooLongException).
        // Catch all of these so that a single bad path does not prevent other candidates
        // from being evaluated.
        string fullPath;
        try
        {
            fullPath = resolver.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException
                                       or ArgumentNullException
                                       or SecurityException
                                       or NotSupportedException
                                       or PathTooLongException)
        {
            return path;
        }

        // Walk up the path collecting non-existent tail segments until we find an
        // ancestor that exists on disk, then resolve that ancestor's symlinks and
        // re-attach the peeled-off segments.
        var tailSegments = new Stack<string>();
        var current = fullPath;

        while (true)
        {
            try
            {
                // ResolveLinkTarget returns null when the entry exists but is not a symlink.
                // It throws when the path does not exist.
                var resolved = resolver.ResolveLinkTarget(current);
                var resolvedBase = resolved is not null ? resolved.FullName : current;

                // Re-attach any non-existent tail segments we peeled off.
                foreach (var segment in tailSegments)
                    resolvedBase = Path.Join(resolvedBase, segment);

                return resolvedBase;
            }
            catch (Exception ex) when (ex is FileNotFoundException
                                           or DirectoryNotFoundException
                                           or IOException
                                           or UnauthorizedAccessException)
            {
                // current doesn't exist or is inaccessible — peel off the last path segment and try its parent.
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
}
