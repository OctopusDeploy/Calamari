using System;

namespace Calamari.Common.Features.Processes.ScriptIsolation;

/// <summary>
/// Abstracts path canonicalisation and case-sensitivity detection so that
/// <see cref="MountedDrives.GetAssociatedDrive"/> can be tested without real
/// filesystem symlinks or platform-specific case behaviour.
/// </summary>
interface IPathResolutionService
{
    /// <summary>
    /// Returns the canonical, fully-qualified form of <paramref name="path"/>,
    /// resolving symlinks and any <c>..</c> / <c>.</c> components.
    /// If the path does not yet exist (cannot resolve a link target), an
    /// absolute, normalised path is still returned via
    /// <see cref="System.IO.Path.GetFullPath"/>.
    /// </summary>
    string ResolvePath(string path);

    /// <summary>
    /// The <see cref="StringComparison"/> that matches the case-sensitivity of
    /// the host filesystem.  Typically <see cref="StringComparison.OrdinalIgnoreCase"/>
    /// on Windows and macOS, and <see cref="StringComparison.Ordinal"/> on Linux.
    /// </summary>
    StringComparison PathComparison { get; }
}
