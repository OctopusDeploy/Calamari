using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
#if USE_NUGET_V2_LIBS
using Calamari.NuGet.Versioning;
#else
using NuGet.Versioning;
#endif

namespace Calamari.Integration.Packages
{
    public abstract class SimplePackageExtractor : IPackageExtractor
    {
        public PackageMetadata GetMetadata(string packageFile)
        {
            var pkg = new PackageMetadata();

            var metaDataSection = ExtractMatchingExtension(packageFile, pkg);

            if (string.IsNullOrEmpty(pkg.FileExtension))
            {
                throw new FileFormatException(string.Format("Unable to determine filetype of file \"{0}\"", packageFile));
            }

            string packageId;
            NuGetVersion version;
            if (!PackageIdentifier.TryParsePackageIdAndVersion(metaDataSection, out packageId, out version))
            {
                throw new FileFormatException(string.Format("Unable to extract the package ID and version from file \"{0}\"", packageFile));
            }

            pkg.Id = packageId;
            pkg.Version = version.ToString();
            return pkg;
        }

        private string ExtractMatchingExtension(string packageFile, PackageMetadata pkg)
        {
            var fileName = Path.GetFileName(packageFile);
            var matchingExtension = Extensions.FirstOrDefault(fileName.EndsWith);
            var metaDataSection = string.Empty;
            if (matchingExtension != null)
            {
                metaDataSection = fileName.Substring(0, fileName.Length - matchingExtension.Length);
            }
            else
            {
                foreach (var ext in Extensions)
                {
                    var match = new Regex("(?<extension>" + Regex.Escape(ext) + ")-[a-z0-9\\-]*$").Match(fileName);
                    if (match.Success)
                    {
                        matchingExtension = match.Groups["extension"].Value;
                        metaDataSection = fileName.Substring(0, match.Index);
                        break;
                    }
                }
            }
            
            pkg.FileExtension = matchingExtension;
            return metaDataSection;
        }


        public abstract int Extract(string packageFile, string directory, bool suppressNestedScriptWarning);
        public abstract string[] Extensions { get; }

        
    }
}