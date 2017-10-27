using System.IO;
using Calamari.Integration.Packages.Metadata;
using Octopus.Core.Resources.Versioning;
#if USE_NUGET_V2_LIBS
using Calamari.NuGet.Versioning;
#else
using NuGet.Versioning;
#endif

namespace Calamari.Integration.Packages
{
    public abstract class SimplePackageExtractor : IPackageExtractor
    {
        public virtual PackageMetadata GetMetadata(string packageFile)
        {

            var metadataAndExtension = PackageIdentifier.ExtractPackageExtensionAndMetadata(packageFile, Extensions);

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
            return pkg;
        }


        public abstract int Extract(string packageFile, string directory, bool suppressNestedScriptWarning);
        public abstract string[] Extensions { get; }

        
    }
}