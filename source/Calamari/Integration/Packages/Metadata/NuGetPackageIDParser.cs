using System.IO;
using Octopus.Core.Resources;
using Octopus.Core.Resources.Versioning;

namespace Calamari.Integration.Packages.Metadata
{
    /// <summary>
    /// A service for extracting metadata from packages that are sourced from a NuGet compatible
    /// feed, including the built in feed.
    /// </summary>
    public class NuGetPackageIDParser : IPackageIDParser
    {
        /// <summary>
        /// NuGet is considered the fallback that will always match the supplied package id
        /// </summary>
        public BasePackageMetadata GetMetadataFromPackageID(string packageID)
        {
            return new BasePackageMetadata()
            {
                Id = packageID,
                FeedType = FeedType.NuGet
            };
        }

        public PackageMetadata GetMetadataFromPackageName(string packageFile, string[] extensions)
        {            
            var metadataAndExtension = PackageIdentifier.ExtractPackageExtensionAndMetadata(packageFile, extensions);

            var idAndVersion = metadataAndExtension.Item1;
            var pkg = new PackageMetadata {FileExtension = metadataAndExtension.Item2};

            if (string.IsNullOrEmpty(pkg.FileExtension))
            {
                throw new FileFormatException($"Unable to determine filetype of file \"{packageFile}\"");
            }

            if (!PackageIdentifier.TryParsePackageIdAndVersion(idAndVersion, out string packageId, out IVersion version))
            {
                throw new FileFormatException($"Unable to extract the package ID and version from file \"{packageFile}\"");
            }

            pkg.Id = packageId;
            pkg.Version = version.ToString();
            pkg.FeedType = FeedType.NuGet;
            pkg.PackageSearchPattern = pkg.Id + "." + pkg.Version + "*";
            return pkg;
        }
    }
}