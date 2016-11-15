using System.Text.RegularExpressions;
#if USE_NUGET_V2_LIBS
using Calamari.NuGet.Versioning;
#else
using NuGet.Versioning;
#endif

namespace Calamari.Integration.Packages
{
    public class PackageIdentifier
    {
        /// <summary>
        /// Takes a string containing a concatenated package ID and version (e.g. a filename or database-key) and 
        /// attempts to parse a package ID and semantic version.  
        /// </summary>
        /// <param name="idAndVersion">The concatenated package ID and version.</param>
        /// <param name="packageId">The parsed package ID</param>
        /// <param name="version">The parsed semantic version</param>
        /// <returns>True if parsing was successful, else False</returns>
        public static bool TryParsePackageIdAndVersion(string idAndVersion, out string packageId, out NuGetVersion version)
        {
            packageId = null;
            version = null;

            const string packageIdPattern = @"(?<packageId>(\w+([_.-]\w+)*?))";
            const string semanticVersionPattern = @"(?<semanticVersion>(\d+(\.\d+){0,3}" // Major Minor Patch
                 + @"(-[0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*)?)" // Pre-release identifiers
                 + @"(\+[0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*)?)"; // Build Metadata

            var match = Regex.Match(idAndVersion, $@"^{packageIdPattern}\.{semanticVersionPattern}$");
            var packageIdMatch = match.Groups["packageId"];
            var versionMatch = match.Groups["semanticVersion"];

            if (!packageIdMatch.Success || !versionMatch.Success)
                return false;

            packageId = packageIdMatch.Value;

            return NuGetVersion.TryParse(versionMatch.Value, out version);
        }
    }
}