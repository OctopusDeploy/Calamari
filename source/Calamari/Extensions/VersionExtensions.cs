#if USE_NUGET_V3_LIBS
using NuGet.Versioning;
using Octopus.Versioning;

namespace Calamari.Extensions
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
                version.ReleaseLabels,
                version.Metadata);
        }
    }
}
#endif