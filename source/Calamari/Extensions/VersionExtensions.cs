#if USE_NUGET_V3_LIBS
using NuGet.Versioning;
using Octopus.Core.Resources.Versioning;

namespace Calamari.Extensions
{
    public static class VersionExtensions
    {
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