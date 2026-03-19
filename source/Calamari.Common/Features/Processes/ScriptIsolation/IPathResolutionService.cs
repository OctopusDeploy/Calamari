using System;
using System.IO;

namespace Calamari.Common.Features.Processes.ScriptIsolation;

/// <summary>
/// Minimal abstraction over the BCL path and symlink primitives used when
/// resolving a path to its canonical form for drive-root matching.
/// Keeping the interface thin allows the resolution <em>logic</em> — housed
/// in <see cref="PathResolutionServiceExtensions.ResolvePath"/> — to be
/// exercised independently of real filesystem state.
/// </summary>
interface IPathResolutionService
{
    /// <summary>
    /// Returns the absolute, normalised form of <paramref name="path"/>
    /// (equivalent to <see cref="Path.GetFullPath(string)"/>).
    /// This operation is purely lexical: no filesystem access, no symlink
    /// resolution.
    /// </summary>
    string GetFullPath(string path);

    /// <summary>
    /// Resolves the final symlink target of <paramref name="path"/>.
    /// <list type="bullet">
    ///   <item><description>
    ///     Returns <c>null</c> if the path exists but is <em>not</em> a symlink.
    ///   </description></item>
    ///   <item><description>
    ///     Returns a <see cref="FileSystemInfo"/> pointing at the real target
    ///     when the path is a symlink (following chains to the final target).
    ///   </description></item>
    ///   <item><description>
    ///     Throws <see cref="FileNotFoundException"/>,
    ///     <see cref="DirectoryNotFoundException"/>, or <see cref="IOException"/>
    ///     when the path does not exist on disk.
    ///   </description></item>
    /// </list>
    /// </summary>
    FileSystemInfo? ResolveLinkTarget(string path);

    /// <summary>
    /// The <see cref="StringComparison"/> appropriate for comparing paths on the
    /// host filesystem.  Typically <see cref="StringComparison.OrdinalIgnoreCase"/>
    /// on Windows and macOS, and <see cref="StringComparison.Ordinal"/> on Linux.
    /// </summary>
    StringComparison PathComparison { get; }
}
