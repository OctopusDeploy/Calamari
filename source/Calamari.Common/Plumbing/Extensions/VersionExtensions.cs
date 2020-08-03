#if USE_NUGET_V3_LIBS
using System;
using NuGet.Versioning;
using Octopus.Versioning;

namespace Calamari.Common.Plumbing.Extensions
{
    public static class VersionExtensions
    {
        /// <summary>
        /// Converts an IVersion to a NuGetVersion.
        /// </summary>
        /// <param name="version">The base version</param>
        /// <returns>The NuGet version</returns>
        public static NuGetVersion ToNuGetVersion(this IVersion version)
        {
            return new NuGetVersion(
                version.Major,
                version.Minor,
                version.Patch,
                version.Revision,
                version.ReleaseLabels,
                version.Metadata);
        }
    }
}
#endif