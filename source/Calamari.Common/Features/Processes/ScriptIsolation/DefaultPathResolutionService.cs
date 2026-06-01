using System;
using System.IO;

namespace Calamari.Common.Features.Processes.ScriptIsolation;

/// <summary>
/// The production implementation of <see cref="IPathResolutionService"/>.
/// Each member is a direct, thin delegate to a BCL primitive — all
/// resolution <em>logic</em> lives in
/// <see cref="PathResolutionServiceExtensions.ResolvePath"/>.
/// </summary>
sealed class DefaultPathResolutionService : IPathResolutionService
{
    DefaultPathResolutionService() { }

    public static readonly DefaultPathResolutionService Instance = new();

    /// <inheritdoc/>
    /// <remarks>Delegates to <see cref="Path.GetFullPath(string)"/>.</remarks>
    public string GetFullPath(string path) => Path.GetFullPath(path);

    /// <inheritdoc/>
    /// <remarks>
    /// Delegates to <see cref="FileInfo.ResolveLinkTarget"/> with
    /// <c>returnFinalTarget: true</c> so that symlink chains are followed
    /// all the way to their ultimate target.
    /// </remarks>
    public FileSystemInfo? ResolveLinkTarget(string path)
        => new FileInfo(path).ResolveLinkTarget(returnFinalTarget: true);

    /// <inheritdoc/>
    public StringComparison PathComparison =>
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
}
