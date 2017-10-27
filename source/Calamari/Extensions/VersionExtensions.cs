#if USE_NUGET_V2_LIBS
using Calamari.NuGet.Versioning;
#else
using NuGet.Versioning;
#endif
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